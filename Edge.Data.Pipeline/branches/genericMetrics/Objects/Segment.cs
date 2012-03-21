using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Data.Objects.Reflection;

namespace Edge.Data.Objects
{
	public class SegmentType
	{
		public Account Account {get; set;}
		public Channel Channel {get; set; }

		public int ID { get; set; }
		public string Name { get; set; }

		public static SegmentType CampaignSegment = new SegmentType() { ID = -875, Name = "Campaign" };
		public static SegmentType AdGroupSegment = new SegmentType() { ID = -876, Name = "AdGroup" };
		public static SegmentType TrackerSegment = new SegmentType() { ID = -977, Name="Tracker" };
	}

	public class Segment: MappedObject
	{
		public SegmentType SegmentType;
		public Account Account;
		public Channel Channel;
		public ObjectStatus Status;

		public string OriginalID;
		public string Value;

		public List<Segment> Segments = new List<Segment>();
		public Dictionary<ExtraField, object> ExtraFields = new Dictionary<ExtraField, object>();

		protected override int GetDynamicTypeID()
		{
			return this.SegmentType.ID;
		}
	}

	public class CampaignSegment : Segment
	{
		/// <summary>
		/// Same as Value.
		/// </summary>
		public string Name
		{
			get { return this.Value; }
			set { this.Value = value; }
		}

		[MappedObjectFieldIndex(4)]
		public double Budget;
	}

	public class AdGroupSegment : Segment
	{
		/// <summary>
		/// Same as Value.
		/// </summary>
		public string Name
		{
			get { return this.Value; }
			set { this.Value = value; }
		}

		[MappedObjectFieldIndex(1)]
		public CampaignSegment Campaign;
	}

	public class TrackerSegment : Segment
	{
	}
}
