﻿///////////////////////////////////////////////////////////////////////////////////////
// Copyright (C) 2006-2015 Esper Team. All rights reserved.                           /
// http://esper.codehaus.org                                                          /
// ---------------------------------------------------------------------------------- /
// The software in this package is published under the terms of the GPL license       /
// a copy of which has been included with this distribution in the license.txt file.  /
///////////////////////////////////////////////////////////////////////////////////////

using com.espertech.esper.client;
using com.espertech.esper.regression.epl;
using com.espertech.esper.support.bean;

using NUnit.Framework;

namespace com.espertech.esper.linq
{
    [TestFixture]
    public class TestLinqStdViews
    {
        private EPServiceProvider _serviceProvider;

        [SetUp]
        public void SetUp()
        {
            Configuration configuration = new Configuration();
            configuration.AddEventType<SupportBean>();
            configuration.AddEventType<SupportPriceEvent>();
            configuration.AddEventType<TestSubselectOrderOfEval.TradeEvent>();

            _serviceProvider = EPServiceProviderManager.GetDefaultProvider(configuration);
            _serviceProvider.Initialize();
        }

        [Test]
        public void TestUnique()
        {
            AssertModelEquality(
                _serviceProvider.From<SupportBean>().Unique(x => x.TheString),
                "select * from com.espertech.esper.support.bean.SupportBean.std:unique(x.TheString)");
        }

        private void AssertModelEquality(EsperQuery<SupportBean> stream, string sample)
        {
            sample = "@IterableUnbound " + sample;

            var sampleModel = _serviceProvider.EPAdministrator.CompileEPL(sample);
            var sampleModelEPL = sampleModel.ToEPL();

            var streamEPL = stream.ObjectModel.ToEPL();

            Assert.That(streamEPL, Is.EqualTo(sampleModelEPL));
        }
    }
}
