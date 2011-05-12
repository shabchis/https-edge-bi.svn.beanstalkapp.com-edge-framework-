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
	}

	public class SegmentValue
	{
		public string OriginalID;
		public string Value;
	}

	/*
	class Program
	{
		void Main()
		{
			Ad ad = new Ad();

			// setting
			ad.Segments[Segment.AdGroupSegment] = new SegmentValue() { Value = "40+ Male" };

			// saving to db
			foreach (var pair in ad.Segments)
			{
				pair.Key.ID; // maps to SegmentID
				pair.Value.OriginalID // maps to SegmentValueOriginalID
				pair.Value.Value // maps to SegmentValue

			}
		}
	}
	*/
}
