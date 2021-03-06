///////////////////////////////////////////////////////////////////////////////////////
// Copyright (C) 2006-2015 Esper Team. All rights reserved.                           /
// http://esper.codehaus.org                                                          /
// ---------------------------------------------------------------------------------- /
// The software in this package is published under the terms of the GPL license       /
// a copy of which has been included with this distribution in the license.txt file.  /
///////////////////////////////////////////////////////////////////////////////////////

using System;

using com.espertech.esper.core.context.util;
using com.espertech.esper.epl.agg.access;
using com.espertech.esper.epl.core;
using com.espertech.esper.epl.expression.core;
using com.espertech.esper.epl.expression;

namespace com.espertech.esper.epl.agg.service
{
    /// <summary>
    /// Implementation for handling aggregation with grouping by group-keys.
    /// </summary>
    public class AggSvcGroupByRefcountedWAccessFactory : AggregationServiceFactoryBase
    {
        protected readonly AggregationAccessorSlotPair[] Accessors;
        protected readonly AggregationStateFactory[] AccessAggregations;
        protected readonly bool IsJoin;

        /// <summary>
        /// Ctor.
        /// </summary>
        /// <param name="evaluators">evaluate the sub-expression within the aggregate function (ie. Sum(4*myNum))</param>
        /// <param name="prototypes">collect the aggregation state that evaluators evaluate to, act as prototypes for new aggregationsaggregation states for each group</param>
        /// <param name="groupKeyBinding">The group key binding.</param>
        /// <param name="accessors">accessor definitions</param>
        /// <param name="accessAggregations">access aggs</param>
        /// <param name="isJoin">true for join, false for single-stream</param>
        public AggSvcGroupByRefcountedWAccessFactory(
            ExprEvaluator[] evaluators,
            AggregationMethodFactory[] prototypes,
            Object groupKeyBinding,
            AggregationAccessorSlotPair[] accessors,
            AggregationStateFactory[] accessAggregations,
            bool isJoin)
            : base(evaluators, prototypes, groupKeyBinding)
        {
            Accessors = accessors;
            AccessAggregations = accessAggregations;
            IsJoin = isJoin;
        }

        public override AggregationService MakeService(
            AgentInstanceContext agentInstanceContext,
            MethodResolutionService methodResolutionService)
        {
            return new AggSvcGroupByRefcountedWAccessImpl(
                Evaluators, Aggregators, GroupKeyBinding, methodResolutionService, Accessors, AccessAggregations, IsJoin);
        }
    }
}