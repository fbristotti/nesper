///////////////////////////////////////////////////////////////////////////////////////
// Copyright (C) 2006-2015 Esper Team. All rights reserved.                           /
// http://esper.codehaus.org                                                          /
// ---------------------------------------------------------------------------------- /
// The software in this package is published under the terms of the GPL license       /
// a copy of which has been included with this distribution in the license.txt file.  /
///////////////////////////////////////////////////////////////////////////////////////

using System;

using com.espertech.esper.client;
using com.espertech.esper.support.bean;
using com.espertech.esper.support.events;

using NUnit.Framework;

namespace com.espertech.esper.events.bean
{
    [TestFixture]
    public class TestArrayMethodPropertyGetter 
    {
        private ArrayMethodPropertyGetter _getter;
        private ArrayMethodPropertyGetter _getterOutOfBounds;
        private EventBean _event;
        private SupportBeanComplexProps _bean;
    
        [SetUp]
        public void SetUp()
        {
            _bean = SupportBeanComplexProps.MakeDefaultBean();
            _event = SupportEventBeanFactory.CreateObject(_bean);
            _getter = MakeGetter(0);
            _getterOutOfBounds = MakeGetter(int.MaxValue);
        }
    
        [Test]
        public void TestCtor()
        {
            try
            {
                MakeGetter(-1);
                Assert.Fail();
            }
            catch (ArgumentException ex)
            {
                // expected
            }
        }
    
        [Test]
        public void TestGet()
        {
            Assert.AreEqual(_bean.ArrayProperty[0], _getter.Get(_event));
            Assert.AreEqual(_bean.ArrayProperty[0], _getter.Get(_event, 0));
    
            Assert.IsNull(_getterOutOfBounds.Get(_event));
    
            try
            {
                _getter.Get(SupportEventBeanFactory.CreateObject(""));
                Assert.Fail();
            }
            catch (PropertyAccessException ex)
            {
                // expected
            }
        }
    
        private ArrayMethodPropertyGetter MakeGetter(int index)
        {
            var method = typeof(SupportBeanComplexProps).GetMethod("GetArrayProperty", new Type[0]);
            return new ArrayMethodPropertyGetter(method, index, SupportEventAdapterService.Service);
        }
    }
}