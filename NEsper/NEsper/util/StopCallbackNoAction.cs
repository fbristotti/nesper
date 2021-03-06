///////////////////////////////////////////////////////////////////////////////////////
// Copyright (C) 2006-2015 Esper Team. All rights reserved.                           /
// http://esper.codehaus.org                                                          /
// ---------------------------------------------------------------------------------- /
// The software in this package is published under the terms of the GPL license       /
// a copy of which has been included with this distribution in the license.txt file.  /
///////////////////////////////////////////////////////////////////////////////////////

using System;

using com.espertech.esper.compat;

namespace com.espertech.esper.util
{
	/// <summary>
	/// Stop callback that performs no action.
	/// </summary>
	public class StopCallbackNoAction
	{
        public readonly static StopCallback INSTANCE = () => { };
	}
} // end of namespace
