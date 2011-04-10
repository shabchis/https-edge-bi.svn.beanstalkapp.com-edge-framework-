using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Pipeline.Objects
{
	public class Segment
	{
		public int ID { get; set; }
		public static Segment AdGroupSegment = new Segment() { ID = -876 };
	}

}
