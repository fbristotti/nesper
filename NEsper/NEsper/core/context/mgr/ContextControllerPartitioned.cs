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
using com.espertech.esper.client.context;
using com.espertech.esper.collection;
using com.espertech.esper.compat.collections;
using com.espertech.esper.core.context.util;

namespace com.espertech.esper.core.context.mgr
{
    public class ContextControllerPartitioned 
        : ContextController
        , ContextControllerPartitionedInstanceCreateCallback
    {
        private readonly int _pathId;
        private readonly ContextControllerLifecycleCallback _activationCallback;
        private readonly ContextControllerPartitionedFactory _factory;

        private readonly IList<ContextControllerPartitionedFilterCallback> _filterCallbacks =
            new List<ContextControllerPartitionedFilterCallback>();

        private readonly IDictionary<Object, ContextControllerInstanceHandle> _partitionKeys =
            new Dictionary<Object, ContextControllerInstanceHandle>().WithNullSupport();
    
        private ContextInternalFilterAddendum _activationFilterAddendum;
        private int _currentSubpathId;
    
        public ContextControllerPartitioned(int pathId, ContextControllerLifecycleCallback activationCallback, ContextControllerPartitionedFactory factory)
        {
            _pathId = pathId;
            _activationCallback = activationCallback;
            _factory = factory;
        }
    
        public void ImportContextPartitions(ContextControllerState state, int pathIdToUse, ContextInternalFilterAddendum filterAddendum, AgentInstanceSelector agentInstanceSelector)
        {
            InitializeFromState(null, null, filterAddendum, state, pathIdToUse, agentInstanceSelector);
        }
    
        public void DeletePath(ContextPartitionIdentifier identifier)
        {
            var partitioned = (ContextPartitionIdentifierPartitioned) identifier;
            _partitionKeys.Remove(GetKeyObjectForLookup(partitioned.Keys));
        }
    
        public void VisitSelectedPartitions(ContextPartitionSelector contextPartitionSelector, ContextPartitionVisitor visitor)
        {
            var nestingLevel = _factory.FactoryContext.NestingLevel;
            if (contextPartitionSelector is ContextPartitionSelectorFiltered) {
                var filtered = (ContextPartitionSelectorFiltered) contextPartitionSelector;
    
                var identifier = new ContextPartitionIdentifierPartitioned();
                foreach (var entry in _partitionKeys) {
                    identifier.ContextPartitionId = entry.Value.ContextPartitionOrPathId;
                    var identifierOA = GetKeyObjectsAccountForMultikey(entry.Key);
                    identifier.Keys = identifierOA;
    
                    if (filtered.Filter(identifier)) {
                        visitor.Visit(nestingLevel, _pathId, _factory.Binding, identifierOA, this, entry.Value);
                    }
                }
                return;
            }
            else if (contextPartitionSelector is ContextPartitionSelectorSegmented) {
                var partitioned = (ContextPartitionSelectorSegmented) contextPartitionSelector;
                if (partitioned.PartitionKeys == null || partitioned.PartitionKeys.IsEmpty()) {
                    return;
                }
                foreach (var keyObjects in partitioned.PartitionKeys) {
                    var key = GetKeyObjectForLookup(keyObjects);
                    var instanceHandle = _partitionKeys.Get(key);
                    if (instanceHandle != null && instanceHandle.ContextPartitionOrPathId != null) {
                        visitor.Visit(nestingLevel, _pathId, _factory.Binding, keyObjects, this, instanceHandle);
                    }
                }
                return;
            }
            else if (contextPartitionSelector is ContextPartitionSelectorById) {
                var filtered = (ContextPartitionSelectorById) contextPartitionSelector;
    
                foreach (var entry in _partitionKeys) {
                    if (filtered.ContextPartitionIds.Contains(entry.Value.ContextPartitionOrPathId)) {
                        visitor.Visit(nestingLevel, _pathId, _factory.Binding, GetKeyObjectsAccountForMultikey(entry.Key), this, entry.Value);
                    }
                }
                return;
            }
            else if (contextPartitionSelector is ContextPartitionSelectorAll) {
                foreach (var entry in _partitionKeys) {
                    visitor.Visit(nestingLevel, _pathId, _factory.Binding, GetKeyObjectsAccountForMultikey(entry.Key), this, entry.Value);
                }
                return;
            }
            throw ContextControllerSelectorUtil.GetInvalidSelector(new Type[]{typeof(ContextPartitionSelectorSegmented)}, contextPartitionSelector);
        }
    
        public void Activate(EventBean optionalTriggeringEvent, IDictionary<String, Object> optionalTriggeringPattern, ContextControllerState controllerState, ContextInternalFilterAddendum filterAddendum, int? importPathId) {
            var factoryContext = _factory.FactoryContext;
            _activationFilterAddendum = filterAddendum;
    
            foreach (var item in _factory.SegmentedSpec.Items) {
                var callback = new ContextControllerPartitionedFilterCallback(factoryContext.ServicesContext, factoryContext.AgentInstanceContextCreate, item, this, filterAddendum);
                _filterCallbacks.Add(callback);
    
                if (optionalTriggeringEvent != null) {
                    var match = StatementAgentInstanceUtil.EvaluateFilterForStatement(factoryContext.ServicesContext, optionalTriggeringEvent, factoryContext.AgentInstanceContextCreate, callback.FilterHandle);
    
                    if (match) {
                        callback.MatchFound(optionalTriggeringEvent, null);
                    }
                }
            }
    
            if (factoryContext.NestingLevel == 1) {
                controllerState = ContextControllerStateUtil.GetRecoveryStates(_factory.StateCache, factoryContext.OutermostContextName);
            }
            if (controllerState == null) {
                return;
            }
    
            int? pathIdToUse = importPathId ?? _pathId;
            InitializeFromState(optionalTriggeringEvent, optionalTriggeringPattern, filterAddendum, controllerState, pathIdToUse.Value, null);
        }

        public ContextControllerFactory Factory
        {
            get { return _factory; }
        }

        public int PathId
        {
            get { return _pathId; }
        }

        public void Deactivate() {
            lock(this)
            {
                var factoryContext = _factory.FactoryContext;
                foreach (var callback in _filterCallbacks)
                {
                    callback.Destroy(factoryContext.ServicesContext.FilterService);
                }
                _partitionKeys.Clear();
                _filterCallbacks.Clear();
                _factory.StateCache.RemoveContextParentPath(
                    factoryContext.OutermostContextName, factoryContext.NestingLevel, _pathId);
            }
        }
    
        public void Create(Object key, EventBean theEvent) {
            lock (this)
            {
                var exists = _partitionKeys.ContainsKey(key);
                if (exists)
                {
                    return;
                }

                _currentSubpathId++;

                // determine properties available for querying
                var factoryContext = _factory.FactoryContext;
                var props = ContextPropertyEventType.GetPartitionBean(
                    factoryContext.ContextName, 0, key, _factory.SegmentedSpec.Items[0].PropertyNames);

                // merge filter addendum, if any
                var filterAddendum = _activationFilterAddendum;
                if (_factory.HasFiltersSpecsNestedContexts)
                {
                    filterAddendum = _activationFilterAddendum != null
                                         ? _activationFilterAddendum.DeepCopy()
                                         : new ContextInternalFilterAddendum();
                    _factory.PopulateContextInternalFilterAddendums(filterAddendum, key);
                }

                var handle = _activationCallback.ContextPartitionInstantiate(
                    null, _currentSubpathId, null, this, theEvent, null, key, props, null, filterAddendum, false,
                    ContextPartitionState.STARTED);

                _partitionKeys.Put(key, handle);

                var keyObjectSaved = GetKeyObjectsAccountForMultikey(key);
                _factory.StateCache.AddContextPath(
                    factoryContext.OutermostContextName, factoryContext.NestingLevel, _pathId, _currentSubpathId,
                    handle.ContextPartitionOrPathId, keyObjectSaved, _factory.Binding);
            }
        }

        private Object[] GetKeyObjectsAccountForMultikey(Object key)
        {
            if (key is MultiKeyUntyped)
            {
                return ((MultiKeyUntyped) key).Keys;
            }
            else
            {
                return new Object[]
                {
                    key
                };
            }
        }

        private Object GetKeyObjectForLookup(Object[] keyObjects)
        {
            if (keyObjects.Length > 1)
            {
                return new MultiKeyUntyped(keyObjects);
            }
            else
            {
                return keyObjects[0];
            }
        }

        private void InitializeFromState(EventBean optionalTriggeringEvent,
                                         IDictionary<String, Object> optionalTriggeringPattern,
                                         ContextInternalFilterAddendum filterAddendum,
                                         ContextControllerState controllerState,
                                         int pathIdToUse,
                                         AgentInstanceSelector agentInstanceSelector)
        {

            var factoryContext = _factory.FactoryContext;
            var states = controllerState.States;

            // restart if there are states
            var maxSubpathId = int.MinValue;
            var childContexts = ContextControllerStateUtil.GetChildContexts(factoryContext, pathIdToUse, states);
            var eventAdapterService = _factory.FactoryContext.ServicesContext.EventAdapterService;

            foreach (var entry in childContexts)
            {
                var keys = (Object[]) _factory.Binding.ByteArrayToObject(entry.Value.Blob, eventAdapterService);
                var mapKey = GetKeyObjectForLookup(keys);

                // merge filter addendum, if any
                var myFilterAddendum = _activationFilterAddendum;
                if (_factory.HasFiltersSpecsNestedContexts)
                {
                    filterAddendum = _activationFilterAddendum != null
                                         ? _activationFilterAddendum.DeepCopy()
                                         : new ContextInternalFilterAddendum();
                    _factory.PopulateContextInternalFilterAddendums(filterAddendum, mapKey);
                }

                // check if exists already
                if (controllerState.IsImported)
                {
                    var existingHandle = _partitionKeys.Get(mapKey);
                    if (existingHandle != null)
                    {
                        _activationCallback.ContextPartitionNavigate(
                            existingHandle, this, controllerState, entry.Value.OptionalContextPartitionId.Value,
                            myFilterAddendum, agentInstanceSelector, entry.Value.Blob);
                        continue;
                    }
                }

                var props = ContextPropertyEventType.GetPartitionBean(
                    factoryContext.ContextName, 0, mapKey, _factory.SegmentedSpec.Items[0].PropertyNames);

                var assignedSubpathId = !controllerState.IsImported ? entry.Key.SubPath : ++_currentSubpathId;
                var handle =
                    _activationCallback.ContextPartitionInstantiate(
                        entry.Value.OptionalContextPartitionId, assignedSubpathId, entry.Key.SubPath, this,
                        optionalTriggeringEvent, optionalTriggeringPattern, mapKey, props, controllerState,
                        myFilterAddendum, factoryContext.IsRecoveringResilient, entry.Value.State);
                _partitionKeys.Put(mapKey, handle);

                if (entry.Key.SubPath > maxSubpathId)
                {
                    maxSubpathId = assignedSubpathId;
                }
            }
            if (!controllerState.IsImported)
            {
                _currentSubpathId = maxSubpathId != int.MinValue ? maxSubpathId : 0;
            }
        }
    }
}
