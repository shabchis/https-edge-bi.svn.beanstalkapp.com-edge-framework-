using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Core.Services2.Scheduling
{
	[Serializable]
	public class SchedulingRule
	{
		//public Guid GuidForUnplaned;
		public SchedulingScope Scope { get; set; }
		public List<int> Days { get; set; }// { get; }
		public List<TimeSpan> Hours { get; set; }// { get; }
		//public TimeSpan Frequency { get; set; }
		public TimeSpan MaxDeviationBefore { get; set; }
		public TimeSpan MaxDeviationAfter { get; set; }
		public DateTime SpecificDateTime { get; set; }
	}
}
