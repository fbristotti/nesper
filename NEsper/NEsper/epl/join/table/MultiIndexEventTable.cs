///////////////////////////////////////////////////////////////////////////////////////
// Copyright (C) 2006-2015 Esper Team. All rights reserved.                           /
// http://esper.codehaus.org                                                          /
// ---------------------------------------------------------------------------------- /
// The software in this package is published under the terms of the GPL license       /
// a copy of which has been included with this distribution in the license.txt file.  /
///////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

using com.espertech.esper.client;
using com.espertech.esper.compat;

namespace com.espertech.esper.epl.join.table
{
    /// <summary>
    /// An event table for holding multiple tables for use when multiple indexes of the 
    /// same dataset must be entered into a cache for use in historical data lookup.
    /// <para/>
    /// Does not allow iteration, adding and removing events. Does allow clearing all tables 
    /// and asking for filled or empty tables. All tables are expected to be filled and empty 
    /// at the same time, reflecting multiple indexes on a single set of data.
    /// </summary>
    public class MultiIndexEventTable : EventTable
    {
        private readonly EventTable[] _tables;
        private readonly EventTableOrganization _organization;

        /// <summary>
        /// Ctor.
        /// </summary>
        /// <param name="tables">tables to hold</param>
        /// <param name="organization">The organization.</param>
        public MultiIndexEventTable(EventTable[] tables, EventTableOrganization organization)
        {
            _tables = tables;
            _organization = organization;
        }

        /// <summary>Returns all tables. </summary>
        /// <value>tables</value>
        public EventTable[] Tables
        {
            get { return _tables; }
        }

        public void AddRemove(EventBean[] newData, EventBean[] oldData) {
            throw new UnsupportedOperationException();
        }
    
        public void Add(EventBean[] events)
        {
            throw new UnsupportedOperationException();
        }
    
        public void Remove(EventBean[] events)
        {
            throw new UnsupportedOperationException();
        }
    
        public void Add(EventBean @event)
        {
            throw new UnsupportedOperationException();
        }

        public void Remove(EventBean @event)
        {
            throw new UnsupportedOperationException();
        }

        public IEnumerator<EventBean> GetEnumerator()
        {
            throw new UnsupportedOperationException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public bool IsEmpty()
        {
            return _tables[0].IsEmpty();
        }

        public void Clear()
        {
            for (int i = 0; i < _tables.Length; i++)
            {
                _tables[i].Clear();
            }
        }
    
        public String ToQueryPlan() {
            StringWriter buf = new StringWriter();
            String delimiter = "";
            foreach (EventTable table in _tables) {
                buf.Write(delimiter);
                buf.Write(table.ToQueryPlan());
                delimiter = ", ";
            }
            return GetType().FullName + " " + buf.ToString();
        }

        public int? NumberOfEvents
        {
            get
            {
                foreach (EventTable table in _tables)
                {
                    var num = table.NumberOfEvents;
                    if (num != null)
                    {
                        return num;
                    }
                }
                return null;
            }
        }

        public int NumKeys
        {
            get { return _tables[0].NumKeys; }
        }

        public object Index
        {
            get
            {
                Object[] indexes = new Object[_tables.Length];
                for (int i = 0; i < indexes.Length; i++)
                {
                    indexes[i] = _tables[i].Index;
                }
                return indexes;
            }
        }

        public EventTableOrganization Organization
        {
            get { return _organization; }
        }
    }
}
