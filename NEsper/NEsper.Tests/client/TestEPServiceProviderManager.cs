///////////////////////////////////////////////////////////////////////////////////////
// Copyright (C) 2006-2015 Esper Team. All rights reserved.                           /
// http://esper.codehaus.org                                                          /
// ---------------------------------------------------------------------------------- /
// The software in this package is published under the terms of the GPL license       /
// a copy of which has been included with this distribution in the license.txt file.  /
///////////////////////////////////////////////////////////////////////////////////////

using com.espertech.esper.core.service;
using com.espertech.esper.support.client;

using NUnit.Framework;

namespace com.espertech.esper.client
{
    [TestFixture]
    public class TestEPServiceProviderManager 
    {
        [Test]
        public void TestGetInstance()
        {
            Configuration configuration = SupportConfigFactory.GetConfiguration();

            EPServiceProvider runtimeDef1 = EPServiceProviderManager.GetDefaultProvider();
            EPServiceProvider runtimeA1 = EPServiceProviderManager.GetProvider("A");
            EPServiceProvider runtimeB = EPServiceProviderManager.GetProvider("B");
            EPServiceProvider runtimeA2 = EPServiceProviderManager.GetProvider("A");
            EPServiceProvider runtimeDef2 = EPServiceProviderManager.GetDefaultProvider(configuration);
            EPServiceProvider runtimeA3 = EPServiceProviderManager.GetProvider("A", configuration);
    
            Assert.NotNull(runtimeDef1);
            Assert.NotNull(runtimeA1);
            Assert.NotNull(runtimeB);
            Assert.IsTrue(runtimeDef1 == runtimeDef2);
            Assert.IsTrue(runtimeA1 == runtimeA2);
            Assert.IsTrue(runtimeA1 == runtimeA3);
            Assert.IsFalse(runtimeA1 == runtimeDef1);
            Assert.IsFalse(runtimeA1 == runtimeB);
    
            Assert.AreEqual("A", runtimeA1.URI);
            Assert.AreEqual("A", runtimeA2.URI);
            Assert.AreEqual("B", runtimeB.URI);
            Assert.AreEqual(EPServiceProviderConstants.DEFAULT_ENGINE_URI, runtimeDef1.URI);
            Assert.AreEqual(EPServiceProviderConstants.DEFAULT_ENGINE_URI, runtimeDef2.URI);
    
            runtimeDef1.Dispose();
            runtimeA1.Dispose();
            runtimeB.Dispose();
            runtimeA2.Dispose();
            runtimeDef2.Dispose();
            runtimeA3.Dispose();
        }
    
        [Test]
        public void TestInvalid()
        {
            Configuration configuration = new Configuration();
            configuration.EngineDefaults.ThreadingConfig.IsInternalTimerEnabled = false;
            configuration.AddEventType("x", "xxx.noclass");
    
            try
            {
                EPServiceProviderManager.GetProvider("someURI", configuration);
                Assert.Fail();
            }
            catch (ConfigurationException ex)
            {
                // Expected
            }
        }
        
        [Test]
        public void TestDefaultNaming()
        {
        	Assert.AreEqual("default", EPServiceProviderConstants.DEFAULT_ENGINE_URI__QUALIFIER);
        	EPServiceProvider epNoArg = EPServiceProviderManager.GetDefaultProvider();
        	EPServiceProvider epDefault = EPServiceProviderManager.GetProvider("default");
        	EPServiceProvider epNull = EPServiceProviderManager.GetProvider(null);
        	
        	Assert.IsTrue(epNoArg == epDefault);
        	Assert.IsTrue(epNull == epDefault);
        	Assert.AreEqual("default", epNull.URI);
        }
    }
}
