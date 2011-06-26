using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Objects
{
	public class Ad
	{
		public Campaign Campaign;
		public string Name;
		public string OriginalID;
		public string DestinationUrl;
		public List<Creative> Creatives=new List<Creative>();
		public List<Target> Targets=new List<Target>();

		/// <summary>
		/// Extra fields for use by channels which have extra metadata per target.
		/// </summary>
		public Dictionary<Segment, SegmentValue> Segments = new Dictionary<Segment, SegmentValue>();

		/// <summary>
		/// Extra fields for use by channels which have extra metadata per target.
		/// </summary>
		public Dictionary<ExtraField, object> ExtraFields = new Dictionary<ExtraField, object>();
	}
}
