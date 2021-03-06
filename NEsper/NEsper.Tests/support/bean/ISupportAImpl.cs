///////////////////////////////////////////////////////////////////////////////////////
// Copyright (C) 2006-2015 Esper Team. All rights reserved.                           /
// http://esper.codehaus.org                                                          /
// ---------------------------------------------------------------------------------- /
// The software in this package is published under the terms of the GPL license       /
// a copy of which has been included with this distribution in the license.txt file.  /
///////////////////////////////////////////////////////////////////////////////////////


using System;

namespace com.espertech.esper.support.bean
{
    [Serializable]
    public class ISupportAImpl : ISupportA
    {
        virtual public String A
        {
            get { return valueA; }
        }

        virtual public String BaseAB
        {
            get { return valueBaseAB; }
        }

        private String valueA;
        private String valueBaseAB;

        public ISupportAImpl(String valueA, String valueBaseAB)
        {
            this.valueA = valueA;
            this.valueBaseAB = valueBaseAB;
        }
    }
}
