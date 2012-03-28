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

		public string Name;
		public string OriginalID;
		public string DestinationUrl;
		public ObjectStatus Status;

		public List<Creative> Creatives = new List<Creative>();
		public List<Target> Targets = new List<Target>();
		public Dictionary<Segment, SegmentObject> Segments = new Dictionary<Segment, SegmentObject>();

		/// <summary>
		/// Extra fields for use by channels which have extra metadata per target.
		/// </summary>
		public Dictionary<ExtraField, object> ExtraFields = new Dictionary<ExtraField, object>();
	}
}
