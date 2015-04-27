///////////////////////////////////////////////////////////////////////////////////////
// Copyright (C) 2006-2015 Esper Team. All rights reserved.                           /
// http://esper.codehaus.org                                                          /
// ---------------------------------------------------------------------------------- /
// The software in this package is published under the terms of the GPL license       /
// a copy of which has been included with this distribution in the license.txt file.  /
///////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;

using com.espertech.esper.client;
using com.espertech.esper.client.annotation;
using com.espertech.esper.collection;
using com.espertech.esper.compat.collections;
using com.espertech.esper.compat.logging;
using com.espertech.esper.core.context.activator;
using com.espertech.esper.core.context.subselect;
using com.espertech.esper.core.context.util;
using com.espertech.esper.core.service;
using com.espertech.esper.epl.agg.service;
using com.espertech.esper.epl.core;
using com.espertech.esper.epl.declexpr;
using com.espertech.esper.epl.expression.baseagg;
using com.espertech.esper.epl.expression.core;
using com.espertech.esper.epl.expression.subquery;
using com.espertech.esper.epl.expression.visitor;
using com.espertech.esper.epl.join.hint;
using com.espertech.esper.epl.join.plan;
using com.espertech.esper.epl.join.table;
using com.espertech.esper.epl.join.util;
using com.espertech.esper.epl.lookup;
using com.espertech.esper.epl.spec;
using com.espertech.esper.metrics.instrumentation;
using com.espertech.esper.util;
using com.espertech.esper.view;

namespace com.espertech.esper.core.start
{
    public class EPStatementStartMethodHelperSubselect
    {
        private static readonly ILog QueryPlanLog = LogManager.GetLogger(AuditPath.QUERYPLAN_LOG);

        private readonly static string MSG_SUBQUERY_REQUIRES_WINDOW = "Subqueries require one or more views to limit the stream, consider declaring a length or time window (applies to correlated or non-fully-aggregated subqueries)";

        internal static SubSelectActivationCollection CreateSubSelectActivation(
            EPServicesContext services,
            StatementSpecCompiled statementSpecContainer,
            StatementContext statementContext,
            EPStatementDestroyCallbackList destroyCallbacks)
        {
            var subSelectStreamDesc = new SubSelectActivationCollection();
            var subselectStreamNumber = 1024;
    
            // Process all subselect expression nodes
            foreach (var subselect in statementSpecContainer.SubSelectExpressions)
            {
                var statementSpec = subselect.StatementSpecCompiled;
                var streamSpec = statementSpec.StreamSpecs[0];
    
                if (streamSpec is FilterStreamSpecCompiled)
                {
                    var filterStreamSpec = (FilterStreamSpecCompiled) statementSpec.StreamSpecs[0];
    
                    subselectStreamNumber++;
    
                    InstrumentationAgent instrumentationAgentSubquery = null;
                    if (InstrumentationHelper.ENABLED) {
                        var eventTypeName = filterStreamSpec.FilterSpec.FilterForEventType.Name;
                        var exprSubselectNode = subselect;
                        instrumentationAgentSubquery = new ProxyInstrumentationAgent() {
                            ProcIndicateQ = () => InstrumentationHelper.Get().QFilterActivationSubselect(eventTypeName, exprSubselectNode),
                            ProcIndicateA = () => InstrumentationHelper.Get().AFilterActivationSubselect()
                        };
                    }
    
                    // Register filter, create view factories
                    ViewableActivator activatorDeactivator = new ViewableActivatorFilterProxy(services, filterStreamSpec.FilterSpec, statementSpec.Annotations, true, instrumentationAgentSubquery, false);
                    var viewFactoryChain = services.ViewService.CreateFactories(subselectStreamNumber, filterStreamSpec.FilterSpec.ResultEventType, filterStreamSpec.ViewSpecs, filterStreamSpec.Options, statementContext);
                    subselect.RawEventType = viewFactoryChain.EventType;
    
                    // Add lookup to list, for later starts
                    subSelectStreamDesc.Add(subselect, new SubSelectActivationHolder(subselectStreamNumber, filterStreamSpec.FilterSpec.ResultEventType, viewFactoryChain, activatorDeactivator, streamSpec));
                }
                else if (streamSpec is TableQueryStreamSpec) {
                    var table = (TableQueryStreamSpec) streamSpec;
                    var metadata = services.TableService.GetTableMetadata(table.TableName);
                    var viewFactoryChain = ViewFactoryChain.FromTypeNoViews(metadata.InternalEventType);
                    subSelectStreamDesc.Add(subselect, new SubSelectActivationHolder(subselectStreamNumber, metadata.InternalEventType, viewFactoryChain, new ViewableActivatorTable(metadata, null), streamSpec));
                    subselect.RawEventType = metadata.InternalEventType;
                    destroyCallbacks.AddCallback(new EPStatementDestroyCallbackTableIdxRef(services.TableService, metadata, statementContext.StatementName).Destroy);
                }
                else
                {
                    var namedSpec = (NamedWindowConsumerStreamSpec) statementSpec.StreamSpecs[0];
                    var processor = services.NamedWindowService.GetProcessor(namedSpec.WindowName);
                    var namedWindowType = processor.TailView.EventType;
                    if (namedSpec.OptPropertyEvaluator != null) {
                        namedWindowType = namedSpec.OptPropertyEvaluator.FragmentEventType;
                    }
    
                    // if named-window index sharing is disabled (the default) or filter expressions are provided then consume the insert-remove stream
                    var disableIndexShare = HintEnum.DISABLE_WINDOW_SUBQUERY_INDEXSHARE.GetHint(statementSpecContainer.Annotations) != null;
                    if (disableIndexShare && processor.IsVirtualDataWindow) {
                        disableIndexShare = false;
                    }
                    if (!namedSpec.FilterExpressions.IsEmpty() || !processor.IsEnableSubqueryIndexShare || disableIndexShare) {
                        var activatorNamedWindow = new ViewableActivatorNamedWindow(processor, namedSpec.FilterExpressions, namedSpec.OptPropertyEvaluator);
                        var viewFactoryChain = services.ViewService.CreateFactories(0, namedWindowType, namedSpec.ViewSpecs, namedSpec.Options, statementContext);
                        subselect.RawEventType = viewFactoryChain.EventType;
                        subSelectStreamDesc.Add(subselect, new SubSelectActivationHolder(subselectStreamNumber, namedWindowType, viewFactoryChain, activatorNamedWindow, streamSpec));
                    }
                    // else if there are no named window stream filter expressions and index sharing is enabled
                    else {
                        var viewFactoryChain = services.ViewService.CreateFactories(0, processor.NamedWindowType, namedSpec.ViewSpecs, namedSpec.Options, statementContext);
                        subselect.RawEventType = processor.NamedWindowType;
                        var activator = new ViewableActivatorSubselectNone();
                        subSelectStreamDesc.Add(subselect, new SubSelectActivationHolder(subselectStreamNumber, namedWindowType, viewFactoryChain, activator, streamSpec));
                    }
                }
            }
    
            return subSelectStreamDesc;
        }

        internal static SubSelectStrategyCollection PlanSubSelect(
            EPServicesContext services,
            StatementContext statementContext,
            bool queryPlanLogging,
            SubSelectActivationCollection subSelectStreamDesc,
            string[] outerStreamNames,
            EventType[] outerEventTypesSelect,
            string[] outerEventTypeNamees,
            ExprDeclaredNode[] declaredExpressions,
            ContextPropertyRegistry contextPropertyRegistry)
        {
            var subqueryNum = -1;
            var collection = new SubSelectStrategyCollection();
    
            IDictionary<ExprDeclaredNode, IList<ExprDeclaredNode>> declaredExpressionCallHierarchy = null;
            if (declaredExpressions.Length > 0) {
                declaredExpressionCallHierarchy = ExprNodeUtility.GetDeclaredExpressionCallHierarchy(declaredExpressions);
            }
    
            foreach (var entry in subSelectStreamDesc.Subqueries)
            {
                subqueryNum++;
                var subselect = entry.Key;
                var subSelectActivation = entry.Value;
    
                try {
                    var factoryDesc = PlanSubSelectInternal(subqueryNum, subselect, subSelectActivation,
                            services, statementContext, queryPlanLogging, subSelectStreamDesc,
                            outerStreamNames, outerEventTypesSelect, outerEventTypeNamees,
                            declaredExpressions, contextPropertyRegistry, declaredExpressionCallHierarchy);
                    collection.Add(subselect, factoryDesc);
                }
                catch (Exception ex) {
                    throw new ExprValidationException("Failed to plan " + GetSubqueryInfoText(subqueryNum, subselect) + ": " + ex.Message, ex);
                }
            }
    
            return collection;
        }

        public static IDictionary<ExprSubselectNode, SubSelectStrategyHolder> StartSubselects(
            EPServicesContext services,
            SubSelectStrategyCollection subSelectStrategyCollection,
            AgentInstanceContext agentInstanceContext,
            IList<StopCallback> stopCallbackList)
        {
            IDictionary<ExprSubselectNode, SubSelectStrategyHolder> subselectStrategies = new Dictionary<ExprSubselectNode, SubSelectStrategyHolder>();
    
            foreach (var subselectEntry in subSelectStrategyCollection.Subqueries)
            {
                var subselectNode = subselectEntry.Key;
                var factoryDesc = subselectEntry.Value;
                var holder = factoryDesc.SubSelectActivationHolder;
    
                // activate view
                var subselectActivationResult = holder.Activator.Activate(agentInstanceContext, true, false);
                stopCallbackList.Add(subselectActivationResult.StopCallback);
    
                // apply returning the strategy instance
                var result = factoryDesc.Factory.Instantiate(services, subselectActivationResult.Viewable, agentInstanceContext, stopCallbackList);
    
                // set aggregation
                var lookupStrategy = result.Strategy;
                var aggregationPreprocessor = result.SubselectAggregationPreprocessor;
    
                // determine strategy
                ExprSubselectStrategy strategy;
                if (aggregationPreprocessor != null) {
                    strategy = new ProxyExprSubselectStrategy {
                        ProcEvaluateMatching = (eventsPerStream, exprEvaluatorContext) =>  {
                            var matchingEvents = lookupStrategy.Lookup(eventsPerStream, exprEvaluatorContext);
                            aggregationPreprocessor.Evaluate(eventsPerStream, matchingEvents, exprEvaluatorContext);
                            return CollectionUtil.SINGLE_NULL_ROW_EVENT_SET;
                        },
                    };
                }
                else {
                    strategy = new ProxyExprSubselectStrategy {
                        ProcEvaluateMatching = (eventsPerStream, exprEvaluatorContext) =>  {
                            return lookupStrategy.Lookup(eventsPerStream, exprEvaluatorContext);
                        },
                    };
                }
    
                var instance = new SubSelectStrategyHolder(strategy,
                        result.SubselectAggregationService,
                        result.PriorNodeStrategies,
                        result.PreviousNodeStrategies,
                        result.SubselectView,
                        result.PostLoad);
                subselectStrategies.Put(subselectNode, instance);
            }
    
            return subselectStrategies;
        }

        private static Pair<EventTableFactory, SubordTableLookupStrategyFactory> DetermineSubqueryIndexFactory(
            ExprNode filterExpr,
            EventType viewableEventType,
            EventType[] outerEventTypes,
            StreamTypeService subselectTypeService,
            bool fullTableScan,
            bool queryPlanLogging,
            ICollection<string> optionalUniqueProps,
            StatementContext statementContext,
            int subqueryNum)
        {
            var result = DetermineSubqueryIndexInternalFactory(filterExpr, viewableEventType, outerEventTypes, subselectTypeService, fullTableScan, optionalUniqueProps, statementContext);
    
            var hook = QueryPlanIndexHookUtil.GetHook(statementContext.Annotations);
            if (queryPlanLogging && (QueryPlanLog.IsInfoEnabled || hook != null)) {
                QueryPlanLog.Info("local index");
                QueryPlanLog.Info("strategy " + result.Second.ToQueryPlan());
                QueryPlanLog.Info("table " + result.First.ToQueryPlan());
                if (hook != null) {
                    string strategyName = result.Second.GetType().Name;
                    hook.Subquery(new QueryPlanIndexDescSubquery(
                            new IndexNameAndDescPair[] {
                                    new IndexNameAndDescPair(null, result.First.EventTableType.Name)
                            }, subqueryNum, strategyName));
                }
            }
    
            return result;
        }

        private static Pair<EventTableFactory, SubordTableLookupStrategyFactory> DetermineSubqueryIndexInternalFactory(
            ExprNode filterExpr,
            EventType viewableEventType,
            EventType[] outerEventTypes,
            StreamTypeService subselectTypeService,
            bool fullTableScan,
            ICollection<string> optionalUniqueProps,
            StatementContext statementContext)
        {
            // No filter expression means full table scan
            if ((filterExpr == null) || fullTableScan)
            {
                var table = new UnindexedEventTableFactory(0);
                var strategy = new SubordFullTableScanLookupStrategyFactory();
                return new Pair<EventTableFactory, SubordTableLookupStrategyFactory>(table, strategy);
            }
    
            // Build a list of streams and indexes
            var excludePlanHint = ExcludePlanHint.GetHint(subselectTypeService.StreamNames, statementContext);
            var joinPropDesc = QueryPlanIndexBuilder.GetJoinProps(filterExpr, outerEventTypes.Length, subselectTypeService.EventTypes, excludePlanHint);
            var hashKeys = joinPropDesc.HashProps;
            var rangeKeys = joinPropDesc.RangeProps;
            IList<SubordPropHashKey> hashKeyList = new List<SubordPropHashKey>(hashKeys.Values);
            IList<SubordPropRangeKey> rangeKeyList = new List<SubordPropRangeKey>(rangeKeys.Values);
            var unique = false;
            ExprNode[] inKeywordSingleIdxKeys = null;
            ExprNode inKeywordMultiIdxKey = null;
    
            // If this is a unique-view and there are unique criteria, use these
            if (optionalUniqueProps != null && !optionalUniqueProps.IsEmpty()) {
                var found = true;
                foreach (var uniqueProp in optionalUniqueProps) {
                    if (!hashKeys.ContainsKey(uniqueProp)) {
                        found = false;
                        break;
                    }
                }
                if (found) {
                    var hashKeysArray = hashKeys.Keys.ToArray();
                    foreach (var hashKey in hashKeysArray) {
                        if (!optionalUniqueProps.Contains(hashKey)) {
                            hashKeys.Remove(hashKey);
                        }
                    }
                    hashKeyList = new List<SubordPropHashKey>(hashKeys.Values);
                    unique = true;
                    rangeKeyList.Clear();
                    rangeKeys.Clear();
                }
            }
    
            // build table (local table)
            EventTableFactory eventTableFactory;
            CoercionDesc hashCoercionDesc;
            CoercionDesc rangeCoercionDesc;
            if (hashKeys.Count != 0 && rangeKeys.IsEmpty())
            {
                var indexedProps = hashKeys.Keys.ToArray();
                hashCoercionDesc = CoercionUtil.GetCoercionTypesHash(viewableEventType, indexedProps, hashKeyList);
                rangeCoercionDesc = new CoercionDesc(false, null);
    
                if (hashKeys.Count == 1) {
                    if (!hashCoercionDesc.IsCoerce) {
                        eventTableFactory = new PropertyIndexedEventTableSingleFactory(0, viewableEventType, indexedProps[0], unique, null);
                    }
                    else {
                        eventTableFactory = new PropertyIndexedEventTableSingleCoerceAddFactory(0, viewableEventType, indexedProps[0], hashCoercionDesc.CoercionTypes[0]);
                    }
                }
                else {
                    if (!hashCoercionDesc.IsCoerce) {
                        eventTableFactory = new PropertyIndexedEventTableFactory(0, viewableEventType, indexedProps, unique, null);
                    }
                    else {
                        eventTableFactory = new PropertyIndexedEventTableCoerceAddFactory(0, viewableEventType, indexedProps, hashCoercionDesc.CoercionTypes);
                    }
                }
            }
            else if (hashKeys.IsEmpty() && rangeKeys.IsEmpty())
            {
                hashCoercionDesc = new CoercionDesc(false, null);
                rangeCoercionDesc = new CoercionDesc(false, null);
                if (joinPropDesc.InKeywordSingleIndex != null) {
                    eventTableFactory = new PropertyIndexedEventTableSingleFactory(0, viewableEventType, joinPropDesc.InKeywordSingleIndex.IndexedProp, unique, null);
                    inKeywordSingleIdxKeys = joinPropDesc.InKeywordSingleIndex.Expressions;
                }
                else if (joinPropDesc.InKeywordMultiIndex != null) {
                    eventTableFactory = new PropertyIndexedEventTableSingleArrayFactory(0, viewableEventType, joinPropDesc.InKeywordMultiIndex.IndexedProp, unique, null);
                    inKeywordMultiIdxKey = joinPropDesc.InKeywordMultiIndex.Expression;
                }
                else {
                    eventTableFactory = new UnindexedEventTableFactory(0);
                }
            }
            else if (hashKeys.IsEmpty() && rangeKeys.Count == 1)
            {
                var indexedProp = rangeKeys.Keys.First();
                var coercionRangeTypes = CoercionUtil.GetCoercionTypesRange(viewableEventType, rangeKeys, outerEventTypes);
                if (!coercionRangeTypes.IsCoerce) {
                    eventTableFactory = new PropertySortedEventTableFactory(0, viewableEventType, indexedProp);
                }
                else {
                    eventTableFactory = new PropertySortedEventTableCoercedFactory(0, viewableEventType, indexedProp, coercionRangeTypes.CoercionTypes[0]);
                }
                hashCoercionDesc = new CoercionDesc(false, null);
                rangeCoercionDesc = coercionRangeTypes;
            }
            else {
                var indexedKeyProps = hashKeys.Keys.ToArray();
                var coercionKeyTypes = SubordPropUtil.GetCoercionTypes(hashKeys.Values);
                var indexedRangeProps = rangeKeys.Keys.ToArray();
                var coercionRangeTypes = CoercionUtil.GetCoercionTypesRange(viewableEventType, rangeKeys, outerEventTypes);
                eventTableFactory = new PropertyCompositeEventTableFactory(0, viewableEventType, indexedKeyProps, coercionKeyTypes, indexedRangeProps, coercionRangeTypes.CoercionTypes);
                hashCoercionDesc = CoercionUtil.GetCoercionTypesHash(viewableEventType, indexedKeyProps, hashKeyList);
                rangeCoercionDesc = coercionRangeTypes;
            }
    
            var subqTableLookupStrategyFactory = SubordinateTableLookupStrategyUtil.GetLookupStrategy(outerEventTypes,
                    hashKeyList, hashCoercionDesc, rangeKeyList, rangeCoercionDesc, inKeywordSingleIdxKeys, inKeywordMultiIdxKey, false);
    
            return new Pair<EventTableFactory, SubordTableLookupStrategyFactory>(eventTableFactory, subqTableLookupStrategyFactory);
        }

        private static StreamTypeService GetDeclaredExprTypeService(
            ExprDeclaredNode[] declaredExpressions,
            IDictionary<ExprDeclaredNode, IList<ExprDeclaredNode>> declaredExpressionCallHierarchy,
            string[] outerStreamNames,
            EventType[] outerEventTypesSelect,
            string engineURI,
            ExprSubselectNode subselect,
            string subexpressionStreamName,
            EventType eventType)
        {
            // Find that subselect within that any of the expression declarations
            foreach (var declaration in declaredExpressions) {
                var visitor = new ExprNodeSubselectDeclaredNoTraverseVisitor(declaration);
                visitor.Reset();
                declaration.Accept(visitor);
                if (!visitor.Subselects.Contains(subselect)) {
                    continue;
                }
    
                // no type service for "alias"
                if (declaration.Prototype.IsAlias) {
                    return null;
                }
    
                // subselect found - compute outer stream names
                // initialize from the outermost provided stream names
                IDictionary<String, int> outerStreamNamesMap = new LinkedHashMap<string, int>();
                var count = 0;
                foreach (var outerStreamName in outerStreamNames) {
                    outerStreamNamesMap.Put(outerStreamName, count++);
                }
    
                // give each declared expression a chance to change the names (unless alias expression)
                var outerStreamNamesForSubselect = outerStreamNamesMap;
                var callers = declaredExpressionCallHierarchy.Get(declaration);
                foreach (var caller in callers) {
                    outerStreamNamesForSubselect = caller.GetOuterStreamNames(outerStreamNamesForSubselect);
                }
                outerStreamNamesForSubselect = declaration.GetOuterStreamNames(outerStreamNamesForSubselect);
    
                // compile a new StreamTypeService for use in validating that particular subselect
                var eventTypes = new EventType[outerStreamNamesForSubselect.Count + 1];
                var streamNames = new string[outerStreamNamesForSubselect.Count + 1];
                eventTypes[0] = eventType;
                streamNames[0] = subexpressionStreamName;
                count = 0;
                foreach (var entry in outerStreamNamesForSubselect) {
                    eventTypes[count+1] = outerEventTypesSelect[entry.Value];
                    streamNames[count+1] = entry.Key;
                    count++;
                }
    
                var availableTypes = new StreamTypeServiceImpl(eventTypes, streamNames, new bool[eventTypes.Length], engineURI, false);
                availableTypes.RequireStreamNames = true;
                return availableTypes;
            }
            return null;
        }

        private static SubSelectStrategyFactoryDesc PlanSubSelectInternal(
            int subqueryNum,
            ExprSubselectNode subselect,
            SubSelectActivationHolder subSelectActivation,
            EPServicesContext services,
            StatementContext statementContext,
            bool queryPlanLogging,
            SubSelectActivationCollection subSelectStreamDesc,
            string[] outerStreamNames,
            EventType[] outerEventTypesSelect,
            string[] outerEventTypeNamees,
            ExprDeclaredNode[] declaredExpressions,
            ContextPropertyRegistry contextPropertyRegistry,
            IDictionary<ExprDeclaredNode, IList<ExprDeclaredNode>> declaredExpressionCallHierarchy)
        {
            if (queryPlanLogging && QueryPlanLog.IsInfoEnabled) {
                QueryPlanLog.Info("For statement '" + statementContext.StatementName + "' subquery " + subqueryNum);
            }
    
            var annotations = statementContext.Annotations;
            var indexHint = IndexHint.GetIndexHint(statementContext.Annotations);
            var statementSpec = subselect.StatementSpecCompiled;
            var filterStreamSpec = statementSpec.StreamSpecs[0];
    
            string subselecteventTypeName = null;
            if (filterStreamSpec is FilterStreamSpecCompiled) {
                subselecteventTypeName = ((FilterStreamSpecCompiled) filterStreamSpec).FilterSpec.FilterForEventTypeName;
            }
            else if (filterStreamSpec is NamedWindowConsumerStreamSpec) {
                subselecteventTypeName = ((NamedWindowConsumerStreamSpec) filterStreamSpec).WindowName;
            }
            else if (filterStreamSpec is TableQueryStreamSpec) {
                subselecteventTypeName = ((TableQueryStreamSpec) filterStreamSpec).TableName;
            }
    
            var viewFactoryChain = subSelectStreamDesc.GetViewFactoryChain(subselect);
            var eventType = viewFactoryChain.EventType;
    
            // determine a stream name unless one was supplied
            var subexpressionStreamName = filterStreamSpec.OptionalStreamName;
            var subselectStreamNumber = subSelectStreamDesc.GetStreamNumber(subselect);
            if (subexpressionStreamName == null) {
                subexpressionStreamName = "$subselect_" + subselectStreamNumber;
            }
            var allStreamNames = new string[outerStreamNames.Length + 1];
            Array.Copy(outerStreamNames, 0, allStreamNames, 1, outerStreamNames.Length);
            allStreamNames[0] = subexpressionStreamName;
    
            // Named windows don't allow data views
            if (filterStreamSpec is NamedWindowConsumerStreamSpec || filterStreamSpec is TableQueryStreamSpec) {
                EPStatementStartMethodHelperValidate.ValidateNoDataWindowOnNamedWindow(viewFactoryChain.FactoryChain);
            }
    
            // Expression declarations are copies of a predefined expression body with their own stream context.
            // Should only be invoked if the subselect belongs to that instance.
            StreamTypeService subselectTypeService = null;
            EventType[] outerEventTypes = null;
    
            // determine subselect type information from the enclosing declared expression, if possibly enclosed
            if (declaredExpressions.Length > 0) {
                subselectTypeService = GetDeclaredExprTypeService(declaredExpressions, declaredExpressionCallHierarchy, outerStreamNames, outerEventTypesSelect, services.EngineURI, subselect, subexpressionStreamName, eventType);
                if (subselectTypeService != null) {
                    outerEventTypes = new EventType[subselectTypeService.EventTypes.Length - 1];
                    Array.Copy(subselectTypeService.EventTypes, 1, outerEventTypes, 0, subselectTypeService.EventTypes.Length - 1);
                }
            }
    
            // Use the override provided by the subselect if present
            if (subselectTypeService == null) {
                if (subselect.FilterSubqueryStreamTypes != null) {
                    subselectTypeService = subselect.FilterSubqueryStreamTypes;
                    outerEventTypes = new EventType[subselectTypeService.EventTypes.Length - 1];
                    Array.Copy(subselectTypeService.EventTypes, 1, outerEventTypes, 0, subselectTypeService.EventTypes.Length - 1);
                }
                else {
                    // Streams event types are the original stream types with the stream zero the subselect stream
                    var namesAndTypes = new LinkedHashMap<string, Pair<EventType, string>>();
                    namesAndTypes.Put(subexpressionStreamName, new Pair<EventType, string>(eventType, subselecteventTypeName));
                    for (var i = 0; i < outerEventTypesSelect.Length; i++)
                    {
                        var pair = new Pair<EventType, string>(outerEventTypesSelect[i], outerEventTypeNamees[i]);
                        namesAndTypes.Put(outerStreamNames[i], pair);
                    }
                    subselectTypeService = new StreamTypeServiceImpl(namesAndTypes, services.EngineURI, true, true);
                    outerEventTypes = outerEventTypesSelect;
                }
            }
    
            // Validate select expression
            var viewResourceDelegateSubselect = new ViewResourceDelegateUnverified();
            var selectClauseSpec = subselect.StatementSpecCompiled.SelectClauseSpec;
            AggregationServiceFactoryDesc aggregationServiceFactoryDesc = null;
            IList<ExprNode> selectExpressions = new List<ExprNode>();
            IList<string> assignedNames = new List<string>();
            var isWildcard = false;
            var isStreamWildcard = false;
            ExprEvaluator[] groupByEvaluators = null;
            bool hasNonAggregatedProperties;
            var aggExprNodes = new List<ExprAggregateNode>(2);

            var evaluatorContextStmt = new ExprEvaluatorContextStatement(statementContext, false);
            var validationContext = new ExprValidationContext(
                subselectTypeService, statementContext.MethodResolutionService, viewResourceDelegateSubselect,
                statementContext.SchedulingService, statementContext.VariableService, statementContext.TableService,
                evaluatorContextStmt, statementContext.EventAdapterService, statementContext.StatementName,
                statementContext.StatementId, statementContext.Annotations, statementContext.ContextDescriptor,
                statementContext.ScriptingService, false, false, true, false, null, false);
            for (int i = 0; i < selectClauseSpec.SelectExprList.Length; i++) {
                SelectClauseElementCompiled element = selectClauseSpec.SelectExprList[i];

                if (element is SelectClauseExprCompiledSpec)
                {
                    // validate
                    SelectClauseExprCompiledSpec compiled = (SelectClauseExprCompiledSpec) element;
                    ExprNode selectExpression = compiled.SelectExpression;
                    selectExpression = ExprNodeUtility.GetValidatedSubtree(ExprNodeOrigin.SELECT, selectExpression, validationContext);

                    selectExpressions.Add(selectExpression);
                    if (compiled.AssignedName == null) {
                        assignedNames.Add(ExprNodeUtility.ToExpressionStringMinPrecedenceSafe(selectExpression));
                    }
                    else {
                        assignedNames.Add(compiled.AssignedName);
                    }

                    // handle aggregation
                    ExprAggregateNodeUtil.GetAggregatesBottomUp(selectExpression, aggExprNodes);

                    if (aggExprNodes.Count > 0)
                    {
                        // This stream (stream 0) properties must either all be under aggregation, or all not be.
                        var propertiesNotAggregated = ExprNodeUtility.GetExpressionProperties(selectExpression, false);
                        foreach (var pair in propertiesNotAggregated)
                        {
                            if (pair.First == 0)
                            {
                                throw new ExprValidationException("Subselect properties must all be within aggregation functions");
                            }
                        }
                    }
                }
                else if (element is SelectClauseElementWildcard) {
                    isWildcard = true;
                }
                else if (element is SelectClauseStreamCompiledSpec) {
                    isStreamWildcard = true;
                }
            }   // end of for loop

            // Figure out all non-aggregated event properties in the select clause (props not under a sum/avg/max aggregation node)
            ExprNodePropOrStreamSet nonAggregatedPropsSelect = ExprNodeUtility.GetNonAggregatedProps(validationContext.StreamTypeService.EventTypes, selectExpressions, contextPropertyRegistry);
            hasNonAggregatedProperties = !nonAggregatedPropsSelect.IsEmpty();

            // Validate and set select-clause names and expressions
            if (!selectExpressions.IsEmpty()) {
                if (isWildcard || isStreamWildcard) {
                    throw new ExprValidationException("Subquery multi-column select does not allow wildcard or stream wildcard when selecting multiple columns.");
                }
                if (selectExpressions.Count > 1 && !subselect.IsAllowMultiColumnSelect) {
                    throw new ExprValidationException("Subquery multi-column select is not allowed in this context.");
                }
                if (statementSpec.GroupByExpressions == null && selectExpressions.Count > 1 &&
                        aggExprNodes.Count > 0 && hasNonAggregatedProperties) {
                    throw new ExprValidationException("Subquery with multi-column select requires that either all or none of the selected columns are under aggregation, unless a group-by clause is also specified");
                }
                subselect.SelectClause = selectExpressions.ToArray();
                subselect.SelectAsNames = assignedNames.ToArray();
            }

            // Handle aggregation
            ExprNodePropOrStreamSet propertiesGroupBy = null;
            if (aggExprNodes.Count > 0)
            {
                if (statementSpec.GroupByExpressions != null && statementSpec.GroupByExpressions.GroupByRollupLevels != null) {
                    throw new ExprValidationException("Group-by expressions in a subselect may not have rollups");
                }
                ExprNode[] theGroupBy = statementSpec.GroupByExpressions == null ? null : statementSpec.GroupByExpressions.GroupByNodes;
                var hasGroupBy = theGroupBy != null && theGroupBy.Length > 0;
                if (hasGroupBy) {
                    ExprNode[] groupByNodes = statementSpec.GroupByExpressions.GroupByNodes;
                    groupByEvaluators = new ExprEvaluator[groupByNodes.Length];

                    // validate group-by
                    for (int i = 0; i < groupByNodes.Length; i++) {
                        groupByNodes[i] = ExprNodeUtility.GetValidatedSubtree(ExprNodeOrigin.GROUPBY, groupByNodes[i], validationContext);
                        groupByEvaluators[i] = groupByNodes[i].ExprEvaluator;
                        String minimal = ExprNodeUtility.IsMinimalExpression(groupByNodes[i]);
                        if (minimal != null) {
                            throw new ExprValidationException("Group-by expressions in a subselect may not have " + minimal);
                        }
                    }

                    // Get a list of event properties being aggregated in the select clause, if any
                    propertiesGroupBy = ExprNodeUtility.GetGroupByPropertiesValidateHasOne(groupByNodes);

                    // Validated all group-by properties come from stream itself
                    ExprNodePropOrStreamDesc firstNonZeroGroupBy = propertiesGroupBy.FirstWithStreamNumNotZero;
                    if (firstNonZeroGroupBy != null) {
                        throw new ExprValidationException("Subselect with group-by requires that group-by properties are provided by the subselect stream only (" + firstNonZeroGroupBy.Textual + " is not)");
                    }

                    // Validate that this is a grouped full-aggregated case
                    String reasonMessage = propertiesGroupBy.NotContainsAll(nonAggregatedPropsSelect);
                    Boolean allInGroupBy = reasonMessage == null;
                    if (!allInGroupBy) {
                        throw new ExprValidationException("Subselect with group-by requires non-aggregated properties in the select-clause to also appear in the group-by clause");
                    }
                }

                // Other stream properties, if there is aggregation, cannot be under aggregation.
                foreach (ExprAggregateNode aggNode in aggExprNodes) {
                    var propertiesNodesAggregated = ExprNodeUtility.GetExpressionProperties(aggNode, true);
                    foreach (var pair in propertiesNodesAggregated) {
                        if (pair.First != 0) {
                            throw new ExprValidationException("Subselect aggregation functions cannot aggregate across correlated properties");
                        }
                    }

                    if (aggNode.OptionalLocalGroupBy != null) {
                        throw new ExprValidationException("Subselect aggregations functions cannot specify a group-by");
                    }
                }

                // determine whether select-clause has grouped-by expressions
                IList<ExprAggregateNodeGroupKey> groupKeyExpressions = null;
                var groupByExpressions = new ExprNode[0];
                if (hasGroupBy) {
                    groupByExpressions = statementSpec.GroupByExpressions.GroupByNodes;
                    for (int i = 0; i < selectExpressions.Count; i++) {
                        ExprNode selectExpression = selectExpressions[i];
                        var revalidate = false;
                        for (int j = 0; j < groupByExpressions.Length; j++) {
                            var foundPairs = ExprNodeUtility.FindExpression(selectExpression, groupByExpressions[j]);
                            foreach (var pair in foundPairs) {
                                var replacement = new ExprAggregateNodeGroupKey(j, groupByEvaluators[j].ReturnType);
                                if (pair.First == null) {
                                    selectExpressions[i] = replacement;
                                }
                                else {
                                    ExprNodeUtility.ReplaceChildNode(pair.First, pair.Second, replacement);
                                    revalidate = true;
                                }
                                if (groupKeyExpressions == null) {
                                    groupKeyExpressions = new List<ExprAggregateNodeGroupKey>();
                                }
                                groupKeyExpressions.Add(replacement);
                            }
                        }

                        // if the select-clause expression changed, revalidate it
                        if (revalidate) {
                            selectExpression = ExprNodeUtility.GetValidatedSubtree(ExprNodeOrigin.SELECT, selectExpression, validationContext);
                            selectExpressions[i] = selectExpression;
                        }
                    }   // end of for loop
                }

                IList<ExprAggregateNode> havingAgg = Collections.GetEmptyList<ExprAggregateNode>();
                IList<ExprAggregateNode> orderByAgg = Collections.GetEmptyList<ExprAggregateNode>();
                aggregationServiceFactoryDesc = AggregationServiceFactoryFactory.GetService(aggExprNodes, Collections.GetEmptyMap<ExprNode, String>(), Collections.GetEmptyList<ExprDeclaredNode>(), groupByExpressions, havingAgg, orderByAgg, groupKeyExpressions, hasGroupBy, annotations, statementContext.VariableService, false, true, statementSpec.FilterRootNode, statementSpec.HavingExprRootNode, statementContext.AggregationServiceFactoryService, subselectTypeService.EventTypes, statementContext.MethodResolutionService, null, statementSpec.OptionalContextName, null, null);

                // assign select-clause
                if (!selectExpressions.IsEmpty()) {
                    subselect.SelectClause = selectExpressions.ToArray();
                    subselect.SelectAsNames = assignedNames.ToArray();
                }
            }
    
            // no aggregation functions allowed in filter
            if (statementSpec.FilterRootNode != null)
            {
                var aggExprNodesFilter = new List<ExprAggregateNode>();
                ExprAggregateNodeUtil.GetAggregatesBottomUp(statementSpec.FilterRootNode, aggExprNodesFilter);
                if (aggExprNodesFilter.Count > 0)
                {
                    throw new ExprValidationException("Aggregation functions are not supported within subquery filters, consider using insert-into instead");
                }
            }
    
            // Validate filter expression, if there is one
            var filterExpr = statementSpec.FilterRootNode;
    
            // add the table filter for tables
            if (filterStreamSpec is TableQueryStreamSpec) {
                var table = (TableQueryStreamSpec) filterStreamSpec;
                filterExpr = ExprNodeUtility.ConnectExpressionsByLogicalAnd(table.FilterExpressions, filterExpr);
            }

            // determine correlated
            var correlatedSubquery = false;
            if (filterExpr != null)
            {
                filterExpr = ExprNodeUtility.GetValidatedSubtree(ExprNodeOrigin.FILTER, filterExpr, validationContext);
                if (filterExpr.ExprEvaluator.ReturnType.GetBoxedType() != typeof(bool?))
                {
                    throw new ExprValidationException("Subselect filter expression must return a boolean value");
                }
    
                // check the presence of a correlated filter, not allowed with aggregation
                var visitor = new ExprNodeIdentifierVisitor(true);
                filterExpr.Accept(visitor);
                var propertiesNodes = visitor.ExprProperties;
                foreach (var pair in propertiesNodes)
                {
                    if (pair.First != 0)
                    {
                        correlatedSubquery = true;
                        break;
                    }
                }
            }
    
            var viewResourceDelegateVerified = EPStatementStartMethodHelperViewResources.VerifyPreviousAndPriorRequirements(new ViewFactoryChain[]{viewFactoryChain}, viewResourceDelegateSubselect);
            var priorNodes = viewResourceDelegateVerified.PerStream[0].PriorRequestsAsList;
            var previousNodes = viewResourceDelegateVerified.PerStream[0].PreviousRequests;
    
            // Set the aggregated flag
            // This must occur here as some analysis of return type depends on aggregated or not.
            if (aggregationServiceFactoryDesc == null) {
                subselect.SubselectAggregationType = ExprSubselectNode.SubqueryAggregationType.NONE;
            }
            else {
                subselect.SubselectAggregationType = hasNonAggregatedProperties ? ExprSubselectNode.SubqueryAggregationType.AGGREGATED : ExprSubselectNode.SubqueryAggregationType.FULLY_AGGREGATED;
            }
    
            // Set the filter.
            var filterExprEval = (filterExpr == null) ? null : filterExpr.ExprEvaluator;
            var assignedFilterExpr = aggregationServiceFactoryDesc != null ? null : filterExprEval;
            subselect.FilterExpr = assignedFilterExpr;
    
            // validation for correlated subqueries against named windows contained-event syntax
            if ((filterStreamSpec is NamedWindowConsumerStreamSpec && correlatedSubquery)) {
                var namedSpec = (NamedWindowConsumerStreamSpec) filterStreamSpec;
                if (namedSpec.OptPropertyEvaluator != null) {
                    throw new ExprValidationException("Failed to validate named window use in subquery, contained-event is only allowed for named windows when not correlated");
                }
            }

            // Validate presence of a data window
            ValidateSubqueryDataWindow(subselect, correlatedSubquery, hasNonAggregatedProperties, propertiesGroupBy, nonAggregatedPropsSelect);
    
            // Determine strategy factories
            //
    
            // handle named window index share first
            if (filterStreamSpec is NamedWindowConsumerStreamSpec) {
                var namedSpec = (NamedWindowConsumerStreamSpec) filterStreamSpec;
                if (namedSpec.FilterExpressions.IsEmpty()) {
                    var processor = services.NamedWindowService.GetProcessor(namedSpec.WindowName);
                    if (processor == null) {
                        throw new ExprValidationException("A named window by name '" + namedSpec.WindowName + "' does not exist");
                    }
    
                    var disableIndexShare = HintEnum.DISABLE_WINDOW_SUBQUERY_INDEXSHARE.GetHint(annotations) != null;
                    if (disableIndexShare && processor.IsVirtualDataWindow) {
                        disableIndexShare = false;
                    }
    
                    if (!disableIndexShare && processor.IsEnableSubqueryIndexShare) {
                        ValidateContextAssociation(statementContext, processor.ContextName, "named window '" + processor.NamedWindowName + "'");
                        if (queryPlanLogging && QueryPlanLog.IsInfoEnabled) {
                            QueryPlanLog.Info("prefering shared index");
                        }
                        var fullTableScanX = HintEnum.SET_NOINDEX.GetHint(annotations) != null;
                        var excludePlanHint = ExcludePlanHint.GetHint(allStreamNames, statementContext);
                        var joinedPropPlan = QueryPlanIndexBuilder.GetJoinProps(filterExpr, outerEventTypes.Length, subselectTypeService.EventTypes, excludePlanHint);
                        SubSelectStrategyFactory factoryX = new SubSelectStrategyFactoryIndexShare(statementContext.StatementName, statementContext.StatementId, subqueryNum, outerEventTypesSelect,
                                processor, null, fullTableScanX, indexHint, joinedPropPlan, filterExprEval, aggregationServiceFactoryDesc, groupByEvaluators, services.TableService, statementContext.Annotations, statementContext.StatementStopService);
                        return new SubSelectStrategyFactoryDesc(subSelectActivation, factoryX, aggregationServiceFactoryDesc, priorNodes, previousNodes);
                    }
                }
            }
    
            // handle table-subselect
            if (filterStreamSpec is TableQueryStreamSpec) {
                var tableSpec = (TableQueryStreamSpec) filterStreamSpec;
                var metadata = services.TableService.GetTableMetadata(tableSpec.TableName);
                if (metadata == null) {
                    throw new ExprValidationException("A table by name '" + tableSpec.TableName + "' does not exist");
                }
    
                ValidateContextAssociation(statementContext, metadata.ContextName, "table '" + tableSpec.TableName + "'");
                var fullTableScanX = HintEnum.SET_NOINDEX.GetHint(annotations) != null;
                var excludePlanHint = ExcludePlanHint.GetHint(allStreamNames, statementContext);
                var joinedPropPlan = QueryPlanIndexBuilder.GetJoinProps(filterExpr, outerEventTypes.Length, subselectTypeService.EventTypes, excludePlanHint);
                SubSelectStrategyFactory factoryX = new SubSelectStrategyFactoryIndexShare(statementContext.StatementName, statementContext.StatementId, subqueryNum, outerEventTypesSelect,
                        null, metadata, fullTableScanX, indexHint, joinedPropPlan, filterExprEval, aggregationServiceFactoryDesc, groupByEvaluators, services.TableService, statementContext.Annotations, statementContext.StatementStopService);
                return new SubSelectStrategyFactoryDesc(subSelectActivation, factoryX, aggregationServiceFactoryDesc, priorNodes, previousNodes);
            }
    
            // determine unique keys, if any
            ICollection<string> optionalUniqueProps = null;
            if (viewFactoryChain.DataWindowViewFactoryCount > 0) {
                optionalUniqueProps = ViewServiceHelper.GetUniqueCandidateProperties(viewFactoryChain.FactoryChain, annotations);
            }
            if (filterStreamSpec is NamedWindowConsumerStreamSpec) {
                var namedSpec = (NamedWindowConsumerStreamSpec) filterStreamSpec;
                var processor = services.NamedWindowService.GetProcessor(namedSpec.WindowName);
                optionalUniqueProps = processor.OptionalUniqueKeyProps;
            }
    
            // handle local stream + named-window-stream
            var fullTableScan = HintEnum.SET_NOINDEX.GetHint(annotations) != null;
            var indexPair = DetermineSubqueryIndexFactory(filterExpr, eventType,
                    outerEventTypes, subselectTypeService, fullTableScan, queryPlanLogging, optionalUniqueProps, statementContext, subqueryNum);
    
            SubSelectStrategyFactory factory = new SubSelectStrategyFactoryLocalViewPreloaded(subqueryNum, subSelectActivation, indexPair, filterExpr, filterExprEval, correlatedSubquery, aggregationServiceFactoryDesc, viewResourceDelegateVerified, groupByEvaluators);
            return new SubSelectStrategyFactoryDesc(subSelectActivation, factory, aggregationServiceFactoryDesc, priorNodes, previousNodes);
        }
    
        public static string GetSubqueryInfoText(int subqueryNum, ExprSubselectNode subselect)
        {
            var text = "subquery number " + (subqueryNum+1);
            var streamRaw = subselect.StatementSpecRaw.StreamSpecs[0];
            if (streamRaw is FilterStreamSpecRaw) {
                text += " querying " + ((FilterStreamSpecRaw) streamRaw).RawFilterSpec.EventTypeName;
            }
            return text;
        }
    
        private static string ValidateContextAssociation(StatementContext statementContext, string entityDeclaredContextName, string entityDesc)
        {
            var optionalProvidedContextName = statementContext.ContextDescriptor == null ? null : statementContext.ContextDescriptor.ContextName;
            if (entityDeclaredContextName != null) {
                if (optionalProvidedContextName == null || !optionalProvidedContextName.Equals(entityDeclaredContextName)) {
                    throw new ExprValidationException("Mismatch in context specification, the context for the " + entityDesc + " is '" +
                            entityDeclaredContextName + "' and the query specifies " +
                            (optionalProvidedContextName == null ? "no context " :
                                    "context '" + optionalProvidedContextName + "'"));
                }
            }
            return null;
        }

        private static void ValidateSubqueryDataWindow(
            ExprSubselectNode subselectNode,
            bool correlatedSubquery,
            bool hasNonAggregatedProperties,
            ExprNodePropOrStreamSet propertiesGroupBy,
            ExprNodePropOrStreamSet nonAggregatedPropsSelect)
        {
            // validation applies only to type+filter subqueries that have no data window
            StreamSpecCompiled streamSpec = subselectNode.StatementSpecCompiled.StreamSpecs[0];
            if (!(streamSpec is FilterStreamSpecCompiled) || streamSpec.ViewSpecs.Length > 0)
            {
                return;
            }

            if (correlatedSubquery)
            {
                throw new ExprValidationException(MSG_SUBQUERY_REQUIRES_WINDOW);
            }

            // we have non-aggregated properties
            if (hasNonAggregatedProperties)
            {
                if (propertiesGroupBy == null)
                {
                    throw new ExprValidationException(MSG_SUBQUERY_REQUIRES_WINDOW);
                }

                var reason = nonAggregatedPropsSelect.NotContainsAll(propertiesGroupBy);
                if (reason != null)
                {
                    throw new ExprValidationException(MSG_SUBQUERY_REQUIRES_WINDOW);
                }
            }
        }
    }
}