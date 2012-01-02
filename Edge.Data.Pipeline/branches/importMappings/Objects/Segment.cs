using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Objects
{
	public class Segment
	{
		public int ID { get; set; }
		public string Name { get; set; }

		public static Segment AdGroupSegment = new Segment() { ID = -876, Name="AdGroup" };
		public static Segment TrackerSegment = new Segment() { ID = -977, Name="Tracker" };
	}

	public class SegmentValue
	{
		public string OriginalID;
		public string Value;

		/// <summary>
		/// Extra fields for use by channels which have additional metadata per segment.
		/// </summary>
		public Dictionary<ExtraField, object> ExtraFields = new Dictionary<ExtraField, object>();
	}
}
