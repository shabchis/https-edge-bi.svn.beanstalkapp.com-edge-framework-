using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Objects
{
	public class Measure
	{
		public int ID;
		public Account Account;
		public string Name;

		public static readonly Measure Cost = new Measure() { ID = -601 };
		public static readonly Measure Impressions = new Measure() { ID = -602 };
		public static readonly Measure UniqueImpressions = new Measure() { ID = -603 };
		public static readonly Measure Clicks = new Measure() { ID = -604 };
		public static readonly Measure UniqueClicks = new Measure() { ID = -605 };
		public static readonly Measure AveragePosition = new Measure() { ID = -606 };
	}
}
