using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Data.Objects.Reflection;

namespace Edge.Data.Objects
{
	public class SegmentType
	{
		public Account Account;
		public Channel Channel;

		public int ID { get; set; }
		public string Name { get; set; }

		public static SegmentType Campaign = new SegmentType() { ID = -875, Name = "Campaign" };
		public static SegmentType AdGroup = new SegmentType() { ID = -876, Name = "AdGroup" };
		public static SegmentType Tracker = new SegmentType() { ID = -977, Name="Tracker" };
	}

	[TypeID(1)]
	public class Segment: MappedType
	{
		public Account Account;
		public Channel Channel;
		public ObjectStatus Status;

		public string OriginalID;

		[FieldIndex(1)]
		public string Value;

		/// <summary>
		/// Used to group segments.
		/// </summary>
		public Dictionary<SegmentType, Segment> Segments = new Dictionary<SegmentType, Segment>();

		/// <summary>
		/// Extra fields for use by channels which have additional metadata per segment.
		/// </summary>
		public Dictionary<ExtraField, object> ExtraFields = new Dictionary<ExtraField, object>();
	}

	public class CampaignSegment : Segment
	{
		// For backwards compatibility
		public string Name
		{
			get { return this.Value; }
			set { this.Value = value; }
		}

		[FieldIndex(4)]
		public double Budget;
	}

	[TypeID(3)]
	public class AdGroupSegment : Segment
	{
		public string Name
		{
			get { return this.Value; }
			set { this.Value = value; }
		}

		[FieldIndex(2)]
		public CampaignSegment Campaign;
	}
}
