///////////////////////////////////////////////////////////////////////////////////////
// Copyright (C) 2006-2015 Esper Team. All rights reserved.                           /
// http://esper.codehaus.org                                                          /
// ---------------------------------------------------------------------------------- /
// The software in this package is published under the terms of the GPL license       /
// a copy of which has been included with this distribution in the license.txt file.  /
///////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Xml;
using System.Xml.XPath;

using com.espertech.esper.client;
using com.espertech.esper.client.scopetest;
using com.espertech.esper.compat;
using com.espertech.esper.metrics.instrumentation;
using com.espertech.esper.support.client;
using com.espertech.esper.support.events;

using NUnit.Framework;

namespace com.espertech.esper.regression.events
{
    [TestFixture]
    public class TestSchemaXMLEventReplace
    {
        public static readonly String CLASSLOADER_SCHEMA_URI = "regression/simpleSchema.xsd";
        public static readonly String CLASSLOADER_SCHEMA_VERSION2_URI = "regression/simpleSchema_version2.xsd";
    
        private EPServiceProvider _epService;
    
        [Test]
        public void TestSchemaReplace()
        {
            ConfigurationEventTypeXMLDOM eventTypeMeta = new ConfigurationEventTypeXMLDOM();
            eventTypeMeta.RootElementName = "simpleEvent";
            String schemaUri = ResourceManager.ResolveResourceURL(CLASSLOADER_SCHEMA_URI).ToString();
            eventTypeMeta.SchemaResource = schemaUri;
            eventTypeMeta.AddNamespacePrefix("ss", "samples:schemas:simpleSchema");
            eventTypeMeta.AddXPathProperty("customProp", "count(/ss:simpleEvent/ss:nested3/ss:nested4)", XPathResultType.Number);
    
            Configuration configuration = SupportConfigFactory.GetConfiguration();
            configuration.AddEventType("TestXMLSchemaType", eventTypeMeta);
    
            _epService = EPServiceProviderManager.GetProvider("TestSchemaXML", configuration);
            _epService.Initialize();
            if (InstrumentationHelper.ENABLED) { InstrumentationHelper.StartTest(_epService, GetType(), GetType().FullName); }
    
            String stmtSelectWild = "select * from TestXMLSchemaType";
            EPStatement wildStmt = _epService.EPAdministrator.CreateEPL(stmtSelectWild);
            EventType type = wildStmt.EventType;
            EventTypeAssertionUtil.AssertConsistency(type);
    
            EPAssertionUtil.AssertEqualsAnyOrder(new []{
                    new EventPropertyDescriptor("nested1", typeof(XmlNode), null, false, false, false, false, true),
                    new EventPropertyDescriptor("prop4", typeof(string), typeof(char), false, false, true, false, false),
                    new EventPropertyDescriptor("nested3", typeof(XmlNode), null, false, false, false, false, true),
                    new EventPropertyDescriptor("customProp", typeof(double?), null, false, false, false, false, false),
            }, type.PropertyDescriptors);
    
            // Update type and replace
            schemaUri = ResourceManager.ResolveResourceURL(CLASSLOADER_SCHEMA_VERSION2_URI).ToString();
            eventTypeMeta.SchemaResource = schemaUri;
            eventTypeMeta.AddXPathProperty("countProp", "count(/ss:simpleEvent/ss:nested3/ss:nested4)", XPathResultType.Number);
            _epService.EPAdministrator.Configuration.ReplaceXMLEventType("TestXMLSchemaType", eventTypeMeta);
    
            wildStmt = _epService.EPAdministrator.CreateEPL(stmtSelectWild);
            type = wildStmt.EventType;
            EventTypeAssertionUtil.AssertConsistency(type);
    
            EPAssertionUtil.AssertEqualsAnyOrder(new []{
                    new EventPropertyDescriptor("nested1", typeof(XmlNode), null, false, false, false, false, true),
                    new EventPropertyDescriptor("prop4", typeof(string), typeof(char), false, false, true, false, false),
                    new EventPropertyDescriptor("prop5", typeof(string), typeof(char), false, false, true, false, false),
                    new EventPropertyDescriptor("nested3", typeof(XmlNode), null, false, false, false, false, true),
                    new EventPropertyDescriptor("customProp", typeof(double?), null, false, false, false, false, false),
                    new EventPropertyDescriptor("countProp", typeof(double?), null, false, false, false, false, false),
            }, type.PropertyDescriptors);

            if (InstrumentationHelper.ENABLED) { InstrumentationHelper.EndTest(); }
        }
    }
}
