using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Objects
{
	public class Segment
	{
		public int ID { get; set; }
		
		public static Segment AdGroupSegment = new Segment() { ID = -876 };
		public static Segment UnifiedTracker = new Segment() { ID = -977 };
	}

	public class SegmentValue
	{
		public string OriginalID;
		public string Value;
	}
}
