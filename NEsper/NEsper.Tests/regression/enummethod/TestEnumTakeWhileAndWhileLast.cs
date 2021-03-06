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
using com.espertech.esper.client.scopetest;
using com.espertech.esper.metrics.instrumentation;
using com.espertech.esper.support.bean;
using com.espertech.esper.support.bean.lambda;
using com.espertech.esper.support.client;

using NUnit.Framework;

namespace com.espertech.esper.regression.enummethod
{
    [TestFixture]
    public class TestEnumTakeWhileAndWhileLast  {
    
        private EPServiceProvider _epService;
        private SupportUpdateListener _listener;
    
        [SetUp]
        public void SetUp() {
    
            Configuration config = SupportConfigFactory.GetConfiguration();
            config.AddEventType("Bean", typeof(SupportBean_ST0_Container));
            config.AddEventType("SupportCollection", typeof(SupportCollection));
            _epService = EPServiceProviderManager.GetDefaultProvider(config);
            _epService.Initialize();
            if (InstrumentationHelper.ENABLED) { InstrumentationHelper.StartTest(_epService, GetType(), GetType().FullName); }
            _listener = new SupportUpdateListener();
        }

        [TearDown]
        public void TearDown()
        {
            if (InstrumentationHelper.ENABLED) { InstrumentationHelper.EndTest(); }
        }
    
        [Test]
        public void TestTakeWhileEvents() {
    
            String[] fields = "val0,val1,val2,val3".Split(',');
            String epl = "select " +
                    "contained.TakeWhile(x => x.P00 > 0) as val0," +
                    "contained.TakeWhile( (x, i) => x.P00 > 0 and i<2) as val1," +
                    "contained.TakeWhileLast(x => x.P00 > 0) as val2," +
                    "contained.TakeWhileLast( (x, i) => x.P00 > 0 and i<2) as val3" +
                    " from Bean";
            EPStatement stmt = _epService.EPAdministrator.CreateEPL(epl);
            stmt.Events += _listener.Update;
            LambdaAssertionUtil.AssertTypes(stmt.EventType, fields, new Type[]
                                                                        {
                                                                            typeof(ICollection<object>), 
                                                                            typeof(ICollection<object>), 
                                                                            typeof(ICollection<object>),
                                                                            typeof(ICollection<object>)
                                                                        });
    
            _epService.EPRuntime.SendEvent(SupportBean_ST0_Container.Make2Value("E1,1", "E2,2", "E3,3"));
            LambdaAssertionUtil.AssertST0Id(_listener, "val0", "E1,E2,E3");
            LambdaAssertionUtil.AssertST0Id(_listener, "val1", "E1,E2");
            LambdaAssertionUtil.AssertST0Id(_listener, "val2", "E1,E2,E3");
            LambdaAssertionUtil.AssertST0Id(_listener, "val3", "E2,E3");
            _listener.Reset();
    
            _epService.EPRuntime.SendEvent(SupportBean_ST0_Container.Make2Value("E1,0", "E2,2", "E3,3"));
            LambdaAssertionUtil.AssertST0Id(_listener, "val0", "");
            LambdaAssertionUtil.AssertST0Id(_listener, "val1", "");
            LambdaAssertionUtil.AssertST0Id(_listener, "val2", "E2,E3");
            LambdaAssertionUtil.AssertST0Id(_listener, "val3", "E2,E3");
            _listener.Reset();
    
            _epService.EPRuntime.SendEvent(SupportBean_ST0_Container.Make2Value("E1,1", "E2,0", "E3,3"));
            LambdaAssertionUtil.AssertST0Id(_listener, "val0", "E1");
            LambdaAssertionUtil.AssertST0Id(_listener, "val1", "E1");
            LambdaAssertionUtil.AssertST0Id(_listener, "val2", "E3");
            LambdaAssertionUtil.AssertST0Id(_listener, "val3", "E3");
            _listener.Reset();
    
            _epService.EPRuntime.SendEvent(SupportBean_ST0_Container.Make2Value("E1,1", "E2,1", "E3,0"));
            LambdaAssertionUtil.AssertST0Id(_listener, "val0", "E1,E2");
            LambdaAssertionUtil.AssertST0Id(_listener, "val1", "E1,E2");
            LambdaAssertionUtil.AssertST0Id(_listener, "val2", "");
            LambdaAssertionUtil.AssertST0Id(_listener, "val3", "");
            _listener.Reset();
    
            _epService.EPRuntime.SendEvent(SupportBean_ST0_Container.Make2Value("E1,1"));
            LambdaAssertionUtil.AssertST0Id(_listener, "val0", "E1");
            LambdaAssertionUtil.AssertST0Id(_listener, "val1", "E1");
            LambdaAssertionUtil.AssertST0Id(_listener, "val2", "E1");
            LambdaAssertionUtil.AssertST0Id(_listener, "val3", "E1");
            _listener.Reset();
    
            _epService.EPRuntime.SendEvent(SupportBean_ST0_Container.Make2Value());
            foreach (String field in fields) {
                LambdaAssertionUtil.AssertST0Id(_listener, field, "");
            }
            _listener.Reset();
    
            _epService.EPRuntime.SendEvent(SupportBean_ST0_Container.Make2Value(null));
            foreach (String field in fields) {
                LambdaAssertionUtil.AssertST0Id(_listener, field, null);
            }
            _listener.Reset();
        }
    
        [Test]
        public void TestTakeWhileScalar() {
    
            String[] fields = "val0,val1,val2,val3".Split(',');
            String epl = "select " +
                    "Strvals.TakeWhile(x => x != 'E1') as val0," +
                    "Strvals.TakeWhile( (x, i) => x != 'E1' and i<2) as val1," +
                    "Strvals.TakeWhileLast(x => x != 'E1') as val2," +
                    "Strvals.TakeWhileLast( (x, i) => x != 'E1' and i<2) as val3" +
                    " from SupportCollection";
            EPStatement stmt = _epService.EPAdministrator.CreateEPL(epl);
            stmt.Events += _listener.Update;
            LambdaAssertionUtil.AssertTypes(stmt.EventType, fields, new Type[]
                                                                        {
                                                                            typeof(ICollection<object>),
                                                                            typeof(ICollection<object>), 
                                                                            typeof(ICollection<object>), 
                                                                            typeof(ICollection<object>)
                                                                        });
    
            _epService.EPRuntime.SendEvent(SupportCollection.MakeString("E1,E2,E3,E4"));
            LambdaAssertionUtil.AssertValuesArrayScalar(_listener, "val0");
            LambdaAssertionUtil.AssertValuesArrayScalar(_listener, "val1");
            LambdaAssertionUtil.AssertValuesArrayScalar(_listener, "val2", "E2", "E3", "E4");
            LambdaAssertionUtil.AssertValuesArrayScalar(_listener, "val3", "E3", "E4");
            _listener.Reset();
        }
    }
}
