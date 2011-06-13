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
		public int DeliveryColumnIndex;

		public static readonly Measure Cost					= new Measure() { ID = -601, DeliveryColumnIndex = 1 };
		public static readonly Measure Impressions			= new Measure() { ID = -602, DeliveryColumnIndex = 2 };
		public static readonly Measure UniqueImpressions	= new Measure() { ID = -603, DeliveryColumnIndex = 3 };
		public static readonly Measure Clicks				= new Measure() { ID = -604, DeliveryColumnIndex = 4 };
		public static readonly Measure UniqueClicks			= new Measure() { ID = -605, DeliveryColumnIndex = 5 };
		public static readonly Measure AveragePosition		= new Measure() { ID = -606, DeliveryColumnIndex = 6 };
	}
}
