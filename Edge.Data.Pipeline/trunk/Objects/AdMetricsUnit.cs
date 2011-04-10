using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Data.Pipeline.Deliveries;

namespace Edge.Data.Pipeline.Objects
{
	public class AdMetricsUnit
	{
		public DateTime TimeStamp;
		public Account Account;
		public Channel Channel;
		public Campaign Campaign;
		public Ad Ad;
		public List<Target> TargetMatches;
		public Tracker Tracker;

		public Currency Currency;

		// Values
		public double Cost;
		public long Impressions;
		public long Clicks;
		public double AveragePosition;
	}
}
