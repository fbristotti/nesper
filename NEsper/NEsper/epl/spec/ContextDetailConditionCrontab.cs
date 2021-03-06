///////////////////////////////////////////////////////////////////////////////////////
// Copyright (C) 2006-2015 Esper Team. All rights reserved.                           /
// http://esper.codehaus.org                                                          /
// ---------------------------------------------------------------------------------- /
// The software in this package is published under the terms of the GPL license       /
// a copy of which has been included with this distribution in the license.txt file.  /
///////////////////////////////////////////////////////////////////////////////////////

using System.Collections.Generic;

using com.espertech.esper.epl.expression.core;
using com.espertech.esper.filter;
using com.espertech.esper.schedule;

namespace com.espertech.esper.epl.spec
{
    public class ContextDetailConditionCrontab : ContextDetailCondition
    {
        private readonly bool _immediate;

        public ContextDetailConditionCrontab(IList<ExprNode> crontab, bool immediate)
        {
            Crontab = crontab;
            _immediate = immediate;
        }

        public IList<ExprNode> Crontab { get; private set; }

        public ScheduleSpec Schedule { get; set; }

        public IList<FilterSpecCompiled> FilterSpecIfAny
        {
            get { return null; }
        }

        public bool IsImmediate
        {
            get { return _immediate; }
        }
    }
}
