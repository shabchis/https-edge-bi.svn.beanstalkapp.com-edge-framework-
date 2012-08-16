using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Core.Services.Configuration;

namespace Edge.Core.Services
{
	[Serializable]
	public class SchedulingRule
	{
		public SchedulingScope Scope { get; set; }
		public List<int> Days { get; set; }
		public List<TimeSpan> Times { get; set; }		
		public TimeSpan MaxDeviationBefore { get; set; }
		public TimeSpan MaxDeviationAfter { get; set; }
		public DateTime SpecificDateTime { get; set; }
	}

	public enum SchedulingScope
	{
		Day,
		Week,
		Month,
		Unplanned
	}
}
