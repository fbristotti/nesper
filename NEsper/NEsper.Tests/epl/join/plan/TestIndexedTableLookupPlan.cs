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
using com.espertech.esper.epl.expression;
using com.espertech.esper.epl.expression.core;
using com.espertech.esper.epl.join.exec.@base;
using com.espertech.esper.epl.join.table;
using com.espertech.esper.epl.virtualdw;
using com.espertech.esper.support.bean;
using com.espertech.esper.support.events;

using NUnit.Framework;

namespace com.espertech.esper.epl.join.plan
{
    [TestFixture]
    public class TestIndexedTableLookupPlan 
    {
        private PropertyIndexedEventTable _propertyMapEventIndex;
        private EventType[] _types;
    
        [SetUp]
        public void SetUp()
        {
            _types = new EventType[] { SupportEventTypeFactory.CreateBeanType(typeof(SupportBean)) };
    
            PropertyIndexedEventTableFactory factory = new PropertyIndexedEventTableFactory(1, _types[0], new String[] {"IntBoxed"}, false, null);
            _propertyMapEventIndex = (PropertyIndexedEventTable) factory.MakeEventTables()[0];
        }
    
        [Test]
        public void TestLookup()
        {
            List<QueryGraphValueEntryHashKeyed> keys = new List<QueryGraphValueEntryHashKeyed>();
            keys.Add(new QueryGraphValueEntryHashKeyedProp(new ExprIdentNodeImpl(_types[0], "IntBoxed", 0), "IntBoxed"));
            IndexedTableLookupPlanMulti spec = new IndexedTableLookupPlanMulti(0, 1, new TableLookupIndexReqKey("idx1"), keys);

            IDictionary<TableLookupIndexReqKey, EventTable>[] indexes = new IDictionary<TableLookupIndexReqKey, EventTable>[2];
            indexes[0] = new Dictionary<TableLookupIndexReqKey, EventTable>();
            indexes[1] = new Dictionary<TableLookupIndexReqKey, EventTable>();
            indexes[1][new TableLookupIndexReqKey("idx1")] = _propertyMapEventIndex;
    
            JoinExecTableLookupStrategy lookupStrategy = spec.MakeStrategy("ABC", "001", null, indexes, _types, new VirtualDWView[2]);
    
            IndexedTableLookupStrategy strategy = (IndexedTableLookupStrategy) lookupStrategy;
            Assert.AreEqual(_types[0], strategy.EventType);
            Assert.AreEqual(_propertyMapEventIndex, strategy.Index);
            Assert.IsTrue(Collections.AreEqual(new String[] {"IntBoxed"}, strategy.Properties));
        }
    }
}
