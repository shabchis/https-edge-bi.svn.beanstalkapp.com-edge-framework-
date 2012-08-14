using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Legacy = Edge.Core.Configuration;

namespace Edge.Core.Scheduling.Objects
{
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

		public static SchedulingRule FromLegacyRule(Legacy.SchedulingRuleElement legacyRule)
		{
			SchedulingRule rule = new SchedulingRule();
			switch (legacyRule.CalendarUnit)
			{
				case Legacy.CalendarUnit.Day:
					rule.Scope = SchedulingScope.Day;
					break;
				case Legacy.CalendarUnit.Month:
					rule.Scope = SchedulingScope.Month;
					break;
				case Legacy.CalendarUnit.Week:
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
