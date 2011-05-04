using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Objects
{
	public class Ad
	{
		public Guid SessionGuid;


		public Campaign Campaign;
		public string Name;
		public string OriginalID;
		public string DestinationUrl;
		public List<Creative> Creatives;
		public List<Target> Targets;
		public Dictionary<Segment, object> Segments;
	}
}
