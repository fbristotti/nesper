///////////////////////////////////////////////////////////////////////////////////////
// Copyright (C) 2006-2015 Esper Team. All rights reserved.                           /
// http://esper.codehaus.org                                                          /
// ---------------------------------------------------------------------------------- /
// The software in this package is published under the terms of the GPL license       /
// a copy of which has been included with this distribution in the license.txt file.  /
///////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;

using com.espertech.esper.client;
using com.espertech.esper.compat.collections;
using com.espertech.esper.epl.agg.service;
using com.espertech.esper.epl.expression.baseagg;
using com.espertech.esper.epl.expression.core;
using com.espertech.esper.epl.table.mgmt;
using com.espertech.esper.events;
using com.espertech.esper.plugin;

namespace com.espertech.esper.epl.expression.accessagg
{
	/// <summary>
	/// Represents a custom aggregation function in an expresson tree.
	/// </summary>
	[Serializable]
    public class ExprPlugInAggMultiFunctionNode
        : ExprAggregateNodeBase 
        , ExprEvaluatorEnumeration
        , ExprAggregateAccessMultiValueNode
        , ExprAggregationPlugInNodeMarker
	{
	    private readonly PlugInAggregationMultiFunctionFactory _pluginAggregationMultiFunctionFactory;
	    private readonly string _functionName;
	    private readonly ConfigurationPlugInAggregationMultiFunction _config;
        [NonSerialized]
	    private ExprPlugInAggMultiFunctionNodeFactory _factory;

        /// <summary>
        /// Ctor.
        /// </summary>
        /// <param name="distinct">flag indicating unique or non-unique value aggregation</param>
        /// <param name="config">The configuration.</param>
        /// <param name="pluginAggregationMultiFunctionFactory">the factory</param>
        /// <param name="functionName">is the aggregation function name</param>
	    public ExprPlugInAggMultiFunctionNode(bool distinct, ConfigurationPlugInAggregationMultiFunction config, PlugInAggregationMultiFunctionFactory pluginAggregationMultiFunctionFactory, string functionName)
	        : base(distinct)
	    {
	        _pluginAggregationMultiFunctionFactory = pluginAggregationMultiFunctionFactory;
	        _functionName = functionName;
	        _config = config;
	    }

	    public override AggregationMethodFactory ValidateAggregationChild(ExprValidationContext validationContext)
	    {
	        ValidatePositionals();
	        return ValidateAggregationParamsWBinding(validationContext, null);
	    }

	    public AggregationMethodFactory ValidateAggregationParamsWBinding(ExprValidationContext validationContext, TableMetadataColumnAggregation tableAccessColumn)
        {
	        // validate using the context provided by the 'outside' streams to determine parameters
	        // at this time 'inside' expressions like 'window(intPrimitive)' are not handled
	        ExprNodeUtility.GetValidatedSubtree(ExprNodeOrigin.AGGPARAM, ChildNodes, validationContext);
	        return ValidateAggregationInternal(validationContext, tableAccessColumn);
	    }

	    private AggregationMethodFactory ValidateAggregationInternal(ExprValidationContext validationContext, TableMetadataColumnAggregation optionalTableColumn)
	    {
            var ctx = new PlugInAggregationMultiFunctionValidationContext(
                _functionName, 
                validationContext.StreamTypeService.EventTypes, PositionalParams, 
                validationContext.StreamTypeService.EngineURIQualifier,
                validationContext.StatementName,
                validationContext, _config, optionalTableColumn, ChildNodes);

	        var handlerPlugin = _pluginAggregationMultiFunctionFactory.ValidateGetHandler(ctx);
	        _factory = new ExprPlugInAggMultiFunctionNodeFactory(this, handlerPlugin);
	        return _factory;
	    }

	    public override string AggregationFunctionName
	    {
	        get { return _functionName; }
	    }

	    public ICollection<EventBean> EvaluateGetROCollectionEvents(EventBean[] eventsPerStream, bool isNewData, ExprEvaluatorContext context)
        {
	        return AggregationResultFuture.GetCollectionOfEvents(Column, eventsPerStream, isNewData, context);
	    }

	    public ICollection<object> EvaluateGetROCollectionScalar(EventBean[] eventsPerStream, bool isNewData, ExprEvaluatorContext context)
        {
	        var result = AggregationResultFuture.GetValue(Column, context.AgentInstanceId, eventsPerStream, isNewData, context);
	        if (result == null) {
	            return null;
	        }

            return result.Unwrap<object>();
	    }

	    public EventType GetEventTypeCollection(EventAdapterService eventAdapterService, string statementId)
        {
	        return _factory.EventTypeCollection;
	    }

	    public Type ComponentTypeCollection
	    {
	        get { return _factory.ComponentTypeCollection; }
	    }

	    public EventType GetEventTypeSingle(EventAdapterService eventAdapterService, string statementId)
        {
	        return _factory.EventTypeSingle;
	    }

	    public EventBean EvaluateGetEventBean(EventBean[] eventsPerStream, bool isNewData, ExprEvaluatorContext context)
        {
	        return AggregationResultFuture.GetEventBean(Column, eventsPerStream, isNewData, context);
	    }

	    protected override bool EqualsNodeAggregateMethodOnly(ExprAggregateNode node)
	    {
	        return false;
	    }
	}
} // end of namespace
