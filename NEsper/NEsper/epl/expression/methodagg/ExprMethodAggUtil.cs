///////////////////////////////////////////////////////////////////////////////////////
// Copyright (C) 2006-2015 Esper Team. All rights reserved.                           /
// http://esper.codehaus.org                                                          /
// ---------------------------------------------------------------------------------- /
// The software in this package is published under the terms of the GPL license       /
// a copy of which has been included with this distribution in the license.txt file.  /
///////////////////////////////////////////////////////////////////////////////////////

using com.espertech.esper.client;
using com.espertech.esper.epl.expression.core;

namespace com.espertech.esper.epl.expression.methodagg
{
	public class ExprMethodAggUtil
    {
	    public static ExprEvaluator GetDefaultEvaluator(ExprNode[] childNodes, bool join, EventType[] typesPerStream)
	    {
	        ExprEvaluator evaluator;
	        if (childNodes.Length > 1) {
	            evaluator = GetMultiNodeEvaluator(childNodes, join, typesPerStream);
	        }
	        else if (childNodes.Length > 0) {
	            if (childNodes[0] is ExprWildcard) {
	                evaluator = GetWildcardEvaluator(typesPerStream, join);
	            }
	            else {
	                // Use the evaluation node under the aggregation node to obtain the aggregation value
	                evaluator = childNodes[0].ExprEvaluator;
	            }
	        }
	        // For aggregation that doesn't evaluate any particular sub-expression, return null on evaluation
	        else {
	            evaluator = new ProxyExprEvaluator
	            {
	                ProcEvaluate = evaluateParams => null,
	                ProcReturnType = () => null,
	            };
	        }
	        return evaluator;
	    }

	    public static ExprEvaluator GetMultiNodeEvaluator(ExprNode[] childNodes, bool join, EventType[] typesPerStream)
	    {
	        var evaluators = new ExprEvaluator[childNodes.Length];

	        // determine constant nodes
	        var count = 0;
	        foreach (var node in childNodes) {
	            if (node is ExprWildcard) {
	                evaluators[count] = GetWildcardEvaluator(typesPerStream, join);
	            }
	            else {
	                evaluators[count] = node.ExprEvaluator;
	            }
	            count++;
	        }

	        return new ProxyExprEvaluator
	        {
	            ProcEvaluate = evaluateParams =>
	            {
	                var values = new object[evaluators.Length];
	                for (var i = 0; i < evaluators.Length; i++) {
	                    values[i] = evaluators[i].Evaluate(evaluateParams);
	                }
	                return values;
	            },

	            ProcReturnType = () => typeof(object[]),
	        };
	    }

	    private static ExprEvaluator GetWildcardEvaluator(EventType[] typesPerStream, bool isJoin)
        {
	        var returnType = typesPerStream != null && typesPerStream.Length > 0 ? typesPerStream[0].UnderlyingType : null;
	        if (isJoin || returnType == null) {
	            throw new ExprValidationException("Invalid use of wildcard (*) for stream selection in a join or an empty from-clause, please use the stream-alias syntax to select a specific stream instead");
	        }
	        return new ProxyExprEvaluator
	        {
	            ProcEvaluate = evaluateParams => {
	                EventBean @event = evaluateParams.EventsPerStream[0];
	                if (@event == null) {
	                    return null;
	                }
	                return @event.Underlying;
	            },
	            ProcReturnType = () => returnType,
	        };
	    }
	}
} // end of namespace
