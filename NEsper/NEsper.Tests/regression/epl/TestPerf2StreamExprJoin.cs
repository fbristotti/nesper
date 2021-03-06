///////////////////////////////////////////////////////////////////////////////////////
// Copyright (C) 2006-2015 Esper Team. All rights reserved.                           /
// http://esper.codehaus.org                                                          /
// ---------------------------------------------------------------------------------- /
// The software in this package is published under the terms of the GPL license       /
// a copy of which has been included with this distribution in the license.txt file.  /
///////////////////////////////////////////////////////////////////////////////////////

using System;

using com.espertech.esper.client;
using com.espertech.esper.client.scopetest;
using com.espertech.esper.compat;
using com.espertech.esper.compat.logging;
using com.espertech.esper.support.bean;
using com.espertech.esper.support.client;

using NUnit.Framework;

namespace com.espertech.esper.regression.epl
{
    [TestFixture]
    public class TestPerf2StreamExprJoin 
    {
        private EPServiceProvider _epService;
        private SupportUpdateListener _listener;
    
        [SetUp]
        public void SetUp()
        {
            Configuration configuration = SupportConfigFactory.GetConfiguration();
            configuration.EngineDefaults.LoggingConfig.IsEnableQueryPlan = true;
            _epService = EPServiceProviderManager.GetDefaultProvider(configuration);
            _epService.Initialize();
            _listener = new SupportUpdateListener();
    
            _epService.EPAdministrator.Configuration.AddEventType("SupportBean", typeof(SupportBean));
            _epService.EPAdministrator.Configuration.AddEventType("SupportBean_ST0", typeof(SupportBean_ST0));
            _epService.EPAdministrator.Configuration.AddEventType("SupportBeanRange", typeof(SupportBeanRange));
        }
    
        [TearDown]
        public void TearDown()
        {
            _listener = null;
        }
    
        [Test]
        public void Test2Stream1Hash2HashConstant()
        {
            String epl;
    
            epl = "select IntPrimitive as val from SupportBean.win:keepall() sb, SupportBean_ST0.std:lastevent() s0 where sb.TheString = 'E6750'";
            RunAssertion(epl, new SupportBean_ST0("E", -1), 6750);
    
            epl = "select IntPrimitive as val from SupportBean_ST0.std:lastevent() s0, SupportBean.win:keepall() sb where sb.TheString = 'E6749'";
            RunAssertion(epl, new SupportBean_ST0("E", -1), 6749);
    
            _epService.EPAdministrator.CreateEPL("create variable string myconst = 'E6751'");
            epl = "select IntPrimitive as val from SupportBean_ST0.std:lastevent() s0, SupportBean.win:keepall() sb where sb.TheString = myconst";
            RunAssertion(epl, new SupportBean_ST0("E", -1), 6751);
    
            epl = "select IntPrimitive as val from SupportBean_ST0.std:lastevent() s0, SupportBean.win:keepall() sb where sb.TheString = (id || '6752')";
            RunAssertion(epl, new SupportBean_ST0("E", -1), 6752);
    
            epl = "select IntPrimitive as val from SupportBean.win:keepall() sb, SupportBean_ST0.std:lastevent() s0 where sb.TheString = (id || '6753')";
            RunAssertion(epl, new SupportBean_ST0("E", -1), 6753);
    
            epl = "select IntPrimitive as val from SupportBean.win:keepall() sb, SupportBean_ST0.std:lastevent() s0 where sb.TheString = 'E6754' and sb.IntPrimitive=6754";
            RunAssertion(epl, new SupportBean_ST0("E", -1), 6754);
    
            epl = "select IntPrimitive as val from SupportBean_ST0.std:lastevent() s0, SupportBean.win:keepall() sb where sb.TheString = (id || '6755') and sb.IntPrimitive=6755";
            RunAssertion(epl, new SupportBean_ST0("E", -1), 6755);
    
            epl = "select IntPrimitive as val from SupportBean_ST0.std:lastevent() s0, SupportBean.win:keepall() sb where sb.IntPrimitive between 6756 and 6756";
            RunAssertion(epl, new SupportBean_ST0("E", -1), 6756);
    
            epl = "select IntPrimitive as val from SupportBean_ST0.std:lastevent() s0, SupportBean.win:keepall() sb where sb.IntPrimitive >= 6757 and IntPrimitive <= 6757";
            RunAssertion(epl, new SupportBean_ST0("E", -1), 6757);
    
            epl = "select IntPrimitive as val from SupportBean_ST0.std:lastevent() s0, SupportBean.win:keepall() sb where sb.TheString = 'E6758' and sb.IntPrimitive >= 6758 and IntPrimitive <= 6758";
            RunAssertion(epl, new SupportBean_ST0("E", -1), 6758);
    
            epl = "select sum(IntPrimitive) as val from SupportBeanRange.std:lastevent() s0, SupportBean.win:keepall() sb where sb.IntPrimitive >= (rangeStart + 1) and IntPrimitive <= (rangeEnd - 1)";
            RunAssertion(epl, new SupportBeanRange("R1", 6000, 6005), 6001+6002+6003+6004);
    
            epl = "select sum(IntPrimitive) as val from SupportBeanRange.std:lastevent() s0, SupportBean.win:keepall() sb where sb.IntPrimitive >= 6001 and IntPrimitive <= (rangeEnd - 1)";
            RunAssertion(epl, new SupportBeanRange("R1", 6000, 6005), 6001+6002+6003+6004);
    
            epl = "select sum(IntPrimitive) as val from SupportBeanRange.std:lastevent() s0, SupportBean.win:keepall() sb where sb.IntPrimitive between (rangeStart + 1) and (rangeEnd - 1)";
            RunAssertion(epl, new SupportBeanRange("R1", 6000, 6005), 6001+6002+6003+6004);
    
            epl = "select sum(IntPrimitive) as val from SupportBeanRange.std:lastevent() s0, SupportBean.win:keepall() sb where sb.IntPrimitive between (rangeStart + 1) and 6004";
            RunAssertion(epl, new SupportBeanRange("R1", 6000, 6005), 6001+6002+6003+6004);
    
            epl = "select sum(IntPrimitive) as val from SupportBeanRange.std:lastevent() s0, SupportBean.win:keepall() sb where sb.IntPrimitive in (6001 : (rangeEnd - 1)]";
            RunAssertion(epl, new SupportBeanRange("R1", 6000, 6005), 6002+6003+6004);
        }
    
        private void RunAssertion(String epl, Object theEvent, Object expected) {
    
            String[] fields = "val".Split(',');
            EPStatement stmt = _epService.EPAdministrator.CreateEPL(epl);
            stmt.Events += _listener.Update;
    
            // preload
            for (int i = 0; i < 10000; i++) {
                _epService.EPRuntime.SendEvent(new SupportBean("E" + i, i));
            }
    
            long startTime = PerformanceObserver.MilliTime;
            for (int i = 0; i < 1000; i++) {
                _epService.EPRuntime.SendEvent(theEvent);
                EPAssertionUtil.AssertProps(_listener.AssertOneGetNewAndReset(), fields, new Object[]{expected});
            }
            long delta = PerformanceObserver.MilliTime - startTime;
            Assert.That(delta, Is.LessThan(500), "delta=" + delta);
            Log.Info("delta=" + delta);
    
            _epService.EPAdministrator.DestroyAllStatements();
        }
    
        private static readonly ILog Log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
    }
}
