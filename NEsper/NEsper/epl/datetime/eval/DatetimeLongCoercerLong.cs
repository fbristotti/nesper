///////////////////////////////////////////////////////////////////////////////////////
// Copyright (C) 2006-2015 Esper Team. All rights reserved.                           /
// http://esper.codehaus.org                                                          /
// ---------------------------------------------------------------------------------- /
// The software in this package is published under the terms of the GPL license       /
// a copy of which has been included with this distribution in the license.txt file.  /
///////////////////////////////////////////////////////////////////////////////////////

using System;

namespace com.espertech.esper.epl.datetime.eval
{
    public class DatetimeLongCoercerLong : DatetimeLongCoercer
    {
        public long Coerce(Object value)
        {
            if (value is long)
                return ((long) value);
            if (value is int)
                return ((int) value);

            throw new ArgumentException("invalid value for datetime", "value");
        }
    }
}
