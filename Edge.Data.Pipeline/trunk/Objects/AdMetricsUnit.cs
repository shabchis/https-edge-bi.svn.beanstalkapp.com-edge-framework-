using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Data.Pipeline;

namespace Edge.Data.Objects
{
	public class AdMetricsUnit
	{
		public DateTime PeriodStart;
		public DateTime PeriodEnd;
		
		public Ad Ad; //table
		public List<Target> TargetMatches; //table
		public Tracker Tracker;

		public Currency Currency;

		// Values
		public double Cost;
		public long Impressions;
		public long Clicks;
		public double AveragePosition;
		public Dictionary<int, double> Conversions; //40 columns conversion1,conversion2...etc where dicitionay key is column number
	}
}
