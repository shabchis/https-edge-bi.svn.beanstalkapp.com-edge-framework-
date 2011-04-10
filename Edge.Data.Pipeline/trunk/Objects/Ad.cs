using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Pipeline.Objects
{
	public class Ad
	{
		public string Name;
		public string OriginalID;
		public string DestinationUrl;
		public List<Creative> Creatives;
		public List<Target> Targets;
		public Dictionary<Segment, object> Segments;
	}
}
