///////////////////////////////////////////////////////////////////////////////////////
// Copyright (C) 2006-2015 Esper Team. All rights reserved.                           /
// http://esper.codehaus.org                                                          /
// ---------------------------------------------------------------------------------- /
// The software in this package is published under the terms of the GPL license       /
// a copy of which has been included with this distribution in the license.txt file.  /
///////////////////////////////////////////////////////////////////////////////////////

using System.Collections.Generic;
using System.Threading;

using com.espertech.esper.client;
using com.espertech.esper.client.scopetest;
using com.espertech.esper.support.bean;
using com.espertech.esper.support.client;

using NUnit.Framework;

namespace com.espertech.esper.multithread
{
    [TestFixture]
    public class TestMTUpdateIStreamSubselect
    {
        private EPServiceProvider _engine;
        private SupportUpdateListener _listener;

        [SetUp]
        public void SetUp()
        {
            Configuration config = SupportConfigFactory.GetConfiguration();
            _engine = EPServiceProviderManager.GetProvider("TestMTUpdate", config);
            _listener = new SupportUpdateListener();
        }

        [TearDown]
        public void TearDown()
        {
            _engine.Initialize();
        }

        [Test]
        public void TestUpdateIStreamSubselect()
        {
            _engine.EPAdministrator.Configuration.AddEventType(typeof(SupportBean));
            _engine.EPAdministrator.Configuration.AddEventType(typeof(SupportBean_S0));
            EPStatement stmt = _engine.EPAdministrator.CreateEPL("Update istream SupportBean as sb " +
                            "set LongPrimitive = (select count(*) from SupportBean_S0.win:keepall() as s0 where s0.P00 = sb.TheString)");
            stmt.Events += _listener.Update;

            // insert 5 data events for each symbol
            int numGroups = 20;
            int numRepeats = 5;
            for (int i = 0; i < numGroups; i++)
            {
                for (int j = 0; j < numRepeats; j++)
                {
                    _engine.EPRuntime.SendEvent(new SupportBean_S0(i, "S0_" + i)); // S0_0 .. S0_19 each has 5 events
                }
            }

            var threads = new List<Thread>();
            for (int i = 0; i < numGroups; i++)
            {
                var group = i;
                var t = new Thread(() => _engine.EPRuntime.SendEvent(new SupportBean("S0_" + group, 1)));
                threads.Add(t);
                t.Start();
            }

            threads.ForEach(t => t.Join());

            // validate results, price must be 5 for each symbol
            Assert.AreEqual(numGroups, _listener.NewDataList.Count);
            foreach (EventBean[] newData in _listener.NewDataList)
            {
                SupportBean result = (SupportBean)(newData[0]).Underlying;
                Assert.AreEqual(numRepeats, result.LongPrimitive);
            }
        }
    }
}
