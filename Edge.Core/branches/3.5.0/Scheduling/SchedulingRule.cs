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

		public SchedulingRule()
		{
		}

		internal static SchedulingRule FromLegacyRule(SchedulingRuleElement legacyRule)
		{
			SchedulingRule rule = new SchedulingRule();
			switch (legacyRule.CalendarUnit)
			{
				case CalendarUnit.Day:
					rule.Scope = SchedulingScope.Day;
					break;
				case CalendarUnit.Month:
					rule.Scope = SchedulingScope.Month;
					break;
				case CalendarUnit.Week:
					rule.Scope = SchedulingScope.Week;
					break;
			}
			//subunits= weekday,monthdays
			rule.Days = legacyRule.SubUnits.ToList();
			rule.Times = legacyRule.ExactTimes.ToList();
			rule.MaxDeviationAfter = legacyRule.MaxDeviation;

			return rule;
		}
	}

	public enum SchedulingScope
	{
		Day,
		Week,
		Month,
		Unplanned
	}
}
