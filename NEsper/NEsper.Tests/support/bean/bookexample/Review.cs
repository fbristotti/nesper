///////////////////////////////////////////////////////////////////////////////////////
// Copyright (C) 2006-2015 Esper Team. All rights reserved.                           /
// http://esper.codehaus.org                                                          /
// ---------------------------------------------------------------------------------- /
// The software in this package is published under the terms of the GPL license       /
// a copy of which has been included with this distribution in the license.txt file.  /
///////////////////////////////////////////////////////////////////////////////////////

using System;

namespace com.espertech.esper.support.bean.bookexample
{
    public class Review
    {
        private int reviewId;
        private String comment;
    
        public Review(int reviewId, String comment)
        {
            this.reviewId = reviewId;
            this.comment = comment;
        }

        public int ReviewId
        {
            get { return reviewId; }
        }

        public string Comment
        {
            get { return comment; }
        }
    }
}
