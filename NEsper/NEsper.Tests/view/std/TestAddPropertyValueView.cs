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
using com.espertech.esper.collection;
using com.espertech.esper.support.events;
using com.espertech.esper.support.view;

using NUnit.Framework;

namespace com.espertech.esper.view.std
{
    [TestFixture]
    public class TestAddPropertyValueView 
    {
        private AddPropertyValueView _myView;
        private SupportMapView _parentView;
        private SupportSchemaNeutralView _childView;
        private EventType _parentEventType;
    
        [SetUp]
        public void SetUp()
        {
            var schema = new Dictionary<String, Object>();
            schema["STDDEV"] = typeof(double?);
            _parentEventType = SupportEventTypeFactory.CreateMapType(schema);
    
            var addProps = new Dictionary<String, Object>();
            addProps["Symbol"] = typeof(string);

            var mergeEventType = SupportEventAdapterService.Service.CreateAnonymousWrapperType(
                "test", _parentEventType, addProps);
    
            // Set up length window view and a test child view
            _myView = new AddPropertyValueView(
                SupportStatementContextFactory.MakeAgentInstanceViewFactoryContext(), 
                new String[] { "Symbol" }, "IBM", mergeEventType);
    
            _parentView = new SupportMapView(schema);
            _parentView.AddView(_myView);
    
            _childView = new SupportSchemaNeutralView();
            _myView.AddView(_childView);
        }
    
        [Test]
        public void TestViewUpdate()
        {
            var eventData = new Dictionary<String, Object>();
    
            // Generate some events
            eventData["STDDEV"] = 100;
            EventBean eventBeanOne = SupportEventBeanFactory.CreateMapFromValues(
                new Dictionary<String, Object>(eventData), _parentEventType);
            eventData["STDDEV"] = 0;
            EventBean eventBeanTwo = SupportEventBeanFactory.CreateMapFromValues(
                new Dictionary<String, Object>(eventData), _parentEventType);
            eventData["STDDEV"] = 99999;
            EventBean eventBeanThree = SupportEventBeanFactory.CreateMapFromValues(
                new Dictionary<String, Object>(eventData), _parentEventType);
    
            // Send events
            _parentView.Update(new EventBean[] {eventBeanOne, eventBeanTwo}, new EventBean[] { eventBeanThree });
    
            // Checks
            EventBean[] newData = _childView.LastNewData;
            Assert.AreEqual(2, newData.Length);
            Assert.AreEqual("IBM", newData[0].Get("Symbol"));
            Assert.AreEqual(100, newData[0].Get("STDDEV"));
            Assert.AreEqual("IBM", newData[1].Get("Symbol"));
            Assert.AreEqual(0, newData[1].Get("STDDEV"));
    
            EventBean[] oldData = _childView.LastOldData;
            Assert.AreEqual(1, oldData.Length);
            Assert.AreEqual("IBM", oldData[0].Get("Symbol"));
            Assert.AreEqual(99999, oldData[0].Get("STDDEV"));
        }
    
        [Test]
        public void TestCopyView()
        {
            AddPropertyValueView copied = (AddPropertyValueView) _myView.CloneView();
            Assert.AreEqual(_myView.PropertyNames, copied.PropertyNames);
            Assert.AreEqual(_myView.PropertyValues, copied.PropertyValues);
        }
    
        // NOTE: the following test was commented out in 4.6.0 and reintroduced in 4.10.0
        
        [Test]
        public void TestAddProperty()
        {
            IDictionary<String, Object> eventData = new Dictionary<String, Object>();
            eventData["STDDEV"] = 100;
            EventBean eventBean = SupportEventBeanFactory.CreateMapFromValues(eventData, _parentEventType);
    
            IDictionary<String, Object> addProps = new Dictionary<String, Object>();
            addProps["test"] = typeof(int);
            EventType newEventType = SupportEventAdapterService.Service.CreateAnonymousWrapperType("test", _parentEventType, addProps);
            EventBean newBean = AddPropertyValueView.AddProperty(
                eventBean, 
                new String[] { "test" }, 
                new MultiKeyUntyped(new Object[] { 2 }), 
                newEventType, 
                SupportEventAdapterService.Service);
    
            Assert.AreEqual(2, newBean.Get("test"));
            Assert.AreEqual(100, newBean.Get("STDDEV"));
        }
    }
}
