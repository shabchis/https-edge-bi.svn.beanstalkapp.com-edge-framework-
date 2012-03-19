using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Objects
{
	public class Ad
	{
		public Account Account;
		public Channel Channel;
		public CampaignSegment Campaign
		{
			get { return (CampaignSegment) this.Segments[SegmentType.Campaign]; }
			set { this.Segments[SegmentType.Campaign] = value; }
		}

		public string Name;
		public string OriginalID;
		public string DestinationUrl;
		public List<Creative> Creatives = new List<Creative>();
		public List<Target> TargetingOptions = new List<Target>();
		public ObjectStatus Status;

		public Dictionary<SegmentType, Segment> Segments = new Dictionary<SegmentType, Segment>();

		/// <summary>
		/// Extra fields for use by channels which have extra metadata per target.
		/// </summary>
		public Dictionary<ExtraField, object> ExtraFields = new Dictionary<ExtraField, object>();
	}
}
