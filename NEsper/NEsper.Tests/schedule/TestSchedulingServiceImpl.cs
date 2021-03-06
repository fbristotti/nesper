///////////////////////////////////////////////////////////////////////////////////////
// Copyright (C) 2006-2015 Esper Team. All rights reserved.                           /
// http://esper.codehaus.org                                                          /
// ---------------------------------------------------------------------------------- /
// The software in this package is published under the terms of the GPL license       /
// a copy of which has been included with this distribution in the license.txt file.  /
///////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;

using com.espertech.esper.compat;
using com.espertech.esper.support.schedule;
using com.espertech.esper.timer;
using com.espertech.esper.type;

using NUnit.Framework;

namespace com.espertech.esper.schedule
{
    [TestFixture]
    public class TestSchedulingServiceImpl
    {
        #region Setup/Teardown

        [SetUp]
        public void SetUp()
        {
            _service = new SchedulingServiceImpl(new TimeSourceServiceImpl());
            _mgmtService = new SchedulingMgmtServiceImpl();

            // 2-by-2 table of buckets and slots
            var buckets = new ScheduleBucket[3];
            _slots = new ScheduleSlot[buckets.Length][];
            for (int i = 0; i < buckets.Length; i++) {
                buckets[i] = _mgmtService.AllocateBucket();
                _slots[i] = new ScheduleSlot[2];
                for (int j = 0; j < _slots[i].Length; j++) {
                    _slots[i][j] = buckets[i].AllocateSlot();
                }
            }

            _callbacks = new SupportScheduleCallback[5];
            for (int i = 0; i < _callbacks.Length; i++) {
                _callbacks[i] = new SupportScheduleCallback();
            }
        }

        #endregion

        private SchedulingServiceImpl _service;
        private SchedulingMgmtServiceImpl _mgmtService;

        private ScheduleSlot[][] _slots;
        private SupportScheduleCallback[] _callbacks;

        private void CheckCallbacks(SupportScheduleCallback[] callbacks, int[] results)
        {
            Assert.IsTrue(callbacks.Length == results.Length);

            for (int i = 0; i < callbacks.Length; i++) {
                Assert.AreEqual(results[i], callbacks[i].ClearAndGetOrderTriggered());
            }
        }

        private void EvaluateSchedule()
        {
            ICollection<ScheduleHandle> handles = new List<ScheduleHandle>();
            _service.Evaluate(handles);

            foreach (ScheduleHandle handle in handles) {
                var cb = (ScheduleHandleCallback) handle;
                cb.ScheduledTrigger(null);
            }
        }

        [Test]
        public void TestAddTwice()
        {
            _service.Add(100, _callbacks[0], _slots[0][0]);
            Assert.IsTrue(_service.IsScheduled(_callbacks[0]));
            _service.Add(100, _callbacks[0], _slots[0][0]);

            _service.Add(ScheduleComputeHelper.ComputeNextOccurance(new ScheduleSpec(), _service.Time, TimeZoneInfo.Local), _callbacks[1], _slots[0][0]);
            _service.Add(ScheduleComputeHelper.ComputeNextOccurance(new ScheduleSpec(), _service.Time, TimeZoneInfo.Local), _callbacks[1], _slots[0][0]);
        }

        [Test]
        public void TestIncorrectRemove()
        {
            var evaluator = new SchedulingServiceImpl(new TimeSourceServiceImpl());
            var callback = new SupportScheduleCallback();
            evaluator.Remove(callback, null);
        }

        [Test]
        public void TestTrigger()
        {
            long startTime = 0;

            _service.Time = 0;

            // Add callbacks
            _service.Add(20, _callbacks[3], _slots[1][1]);
            _service.Add(20, _callbacks[2], _slots[1][0]);
            _service.Add(20, _callbacks[1], _slots[0][1]);
            _service.Add(21, _callbacks[0], _slots[0][0]);
            Assert.IsTrue(_service.IsScheduled(_callbacks[3]));
            Assert.IsTrue(_service.IsScheduled(_callbacks[0]));

            // Evaluate before the within time, expect not results
            startTime += 19;
            _service.Time = startTime;
            EvaluateSchedule();
            CheckCallbacks(_callbacks, new[] {0, 0, 0, 0, 0});
            Assert.IsTrue(_service.IsScheduled(_callbacks[3]));

            // Evaluate exactly on the within time, expect a result
            startTime += 1;
            _service.Time = startTime;
            EvaluateSchedule();
            CheckCallbacks(_callbacks, new[] {0, 1, 2, 3, 0});
            Assert.IsFalse(_service.IsScheduled(_callbacks[3]));

            // Evaluate after already evaluated once, no result
            startTime += 1;
            _service.Time = startTime;
            EvaluateSchedule();
            CheckCallbacks(_callbacks, new[] {4, 0, 0, 0, 0});
            Assert.IsFalse(_service.IsScheduled(_callbacks[3]));

            startTime += 1;
            _service.Time = startTime;
            EvaluateSchedule();
            Assert.AreEqual(0, _callbacks[3].ClearAndGetOrderTriggered());

            // Adding the same callback more than once should cause an exception
            _service.Add(20, _callbacks[0], _slots[0][0]);
            _service.Add(28, _callbacks[0], _slots[0][0]);

            _service.Remove(_callbacks[0], _slots[0][0]);

            _service.Add(20, _callbacks[2], _slots[1][0]);
            _service.Add(25, _callbacks[1], _slots[0][1]);
            _service.Remove(_callbacks[1], _slots[0][1]);
            _service.Add(21, _callbacks[0], _slots[0][0]);
            _service.Add(21, _callbacks[3], _slots[1][1]);
            _service.Add(20, _callbacks[1], _slots[0][1]);
            SupportScheduleCallback.SetCallbackOrderNum(0);

            startTime += 20;
            _service.Time = startTime;
            EvaluateSchedule();
            CheckCallbacks(_callbacks, new[] {0, 1, 2, 0, 0});

            startTime += 1;
            _service.Time = startTime;
            EvaluateSchedule();
            CheckCallbacks(_callbacks, new[] {3, 0, 0, 4, 0});

            _service.Time = startTime + int.MaxValue;
            EvaluateSchedule();
            CheckCallbacks(_callbacks, new[] {0, 0, 0, 0, 0});
        }

        [Test]
        public void TestWaitAndSpecTogether()
        {
            var startDate = new DateTime(2004, 12, 9, 15, 27, 10, 500, DateTimeKind.Local);
            var startTime = startDate.UtcMillis();

            _service.Time = startTime;

            // Add a specification
            var spec = new ScheduleSpec();
            spec.AddValue(ScheduleUnit.MONTHS, 12);
            spec.AddValue(ScheduleUnit.DAYS_OF_MONTH, 9);
            spec.AddValue(ScheduleUnit.HOURS, 15);
            spec.AddValue(ScheduleUnit.MINUTES, 27);
            spec.AddValue(ScheduleUnit.SECONDS, 20);

            _service.Add(ScheduleComputeHelper.ComputeDeltaNextOccurance(spec, _service.Time, TimeZoneInfo.Local), _callbacks[3], _slots[1][1]);

            spec.AddValue(ScheduleUnit.SECONDS, 15);
            _service.Add(ScheduleComputeHelper.ComputeDeltaNextOccurance(spec, _service.Time, TimeZoneInfo.Local), _callbacks[4], _slots[2][0]);

            // Add some more callbacks
            _service.Add(5000, _callbacks[0], _slots[0][0]);
            _service.Add(10000, _callbacks[1], _slots[0][1]);
            _service.Add(15000, _callbacks[2], _slots[1][0]);

            // Now send a times reflecting various seconds later and check who got a callback
            _service.Time = startTime + 1000;
            SupportScheduleCallback.SetCallbackOrderNum(0);
            EvaluateSchedule();
            CheckCallbacks(_callbacks, new[] {0, 0, 0, 0, 0});

            _service.Time = startTime + 2000;
            EvaluateSchedule();
            CheckCallbacks(_callbacks, new[] {0, 0, 0, 0, 0});

            _service.Time = startTime + 4000;
            EvaluateSchedule();
            CheckCallbacks(_callbacks, new[] {0, 0, 0, 0, 0});

            _service.Time = startTime + 5000;
            EvaluateSchedule();
            CheckCallbacks(_callbacks, new[] {1, 0, 0, 0, 2});

            _service.Time = startTime + 9000;
            EvaluateSchedule();
            CheckCallbacks(_callbacks, new[] {0, 0, 0, 0, 0});

            _service.Time = startTime + 10000;
            EvaluateSchedule();
            CheckCallbacks(_callbacks, new[] {0, 3, 0, 4, 0});

            _service.Time = startTime + 11000;
            EvaluateSchedule();
            CheckCallbacks(_callbacks, new[] {0, 0, 0, 0, 0});

            _service.Time = startTime + 15000;
            EvaluateSchedule();
            CheckCallbacks(_callbacks, new[] {0, 0, 5, 0, 0});

            _service.Time = startTime + int.MaxValue;
            EvaluateSchedule();
            CheckCallbacks(_callbacks, new[] {0, 0, 0, 0, 0});
        }
    }
}
