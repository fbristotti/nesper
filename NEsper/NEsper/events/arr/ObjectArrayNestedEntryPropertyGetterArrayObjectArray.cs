///////////////////////////////////////////////////////////////////////////////////////
// Copyright (C) 2006-2015 Esper Team. All rights reserved.                           /
// http://esper.codehaus.org                                                          /
// ---------------------------------------------------------------------------------- /
// The software in this package is published under the terms of the GPL license       /
// a copy of which has been included with this distribution in the license.txt file.  /
///////////////////////////////////////////////////////////////////////////////////////

using System;

using com.espertech.esper.client;

namespace com.espertech.esper.events.arr
{
    public class ObjectArrayNestedEntryPropertyGetterArrayObjectArray : ObjectArrayNestedEntryPropertyGetterBase {
    
        private readonly int _index;
        private readonly ObjectArrayEventPropertyGetter _getter;
    
        public ObjectArrayNestedEntryPropertyGetterArrayObjectArray(int propertyIndex, EventType fragmentType, EventAdapterService eventAdapterService, int index, ObjectArrayEventPropertyGetter getter)
            : base(propertyIndex, fragmentType, eventAdapterService)
        {
            _index = index;
            _getter = getter;
        }
    
        public override Object HandleNestedValue(Object value) {
            return BaseNestableEventUtil.HandleNestedValueArrayWithObjectArray(value, _index, _getter);
        }
    
        public override Object HandleNestedValueFragment(Object value) {
            return BaseNestableEventUtil.HandleNestedValueArrayWithObjectArrayFragment(value, _index, _getter, FragmentType, EventAdapterService);
        }
    }
}
