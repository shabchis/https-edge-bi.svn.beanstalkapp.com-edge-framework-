using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Legacy = Edge.Core.Configuration;

namespace Edge.Core.Scheduling.Objects
{
	public class SchedulingRule
	{
		public Guid GuidForUnplanned { get; private set; }
		public SchedulingScope Scope { get; set; }
		public List<int> Days { get; set; }
		public List<TimeSpan> Times { get; set; }		
		public TimeSpan MaxDeviationBefore { get; set; }
		public TimeSpan MaxDeviationAfter { get; set; }
		public DateTime SpecificDateTime { get; set; }

		private SchedulingRule()
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

		public static SchedulingRule CreateUnplanned()
		{
			return new SchedulingRule()
			{
				Scope = SchedulingScope.Unplanned,
				MaxDeviationAfter = new TimeSpan(0, 3, 0),
				Days = new List<int>(),
				Times = new List<TimeSpan>() { new TimeSpan(0, 0, 0, 0) },
				GuidForUnplanned = Guid.NewGuid(),
				SpecificDateTime = DateTime.Now
			};
		}
	}
	public class SchedulingData
	{
		internal Guid Guid;
		public ServiceConfiguration Configuration;
		public SchedulingRule Rule;
		public Profile Profile;
		public int SelectedDay;
		public TimeSpan SelectedHour;
		public DateTime TimeToRun;
		public Legacy.ActiveServiceElement LegacyConfiguration;
		public int Priority;
		public SchedulingData()
		{
			Guid = Guid.NewGuid();
		}
		public override string ToString()
		{
			string uniqueKey = string.Empty;			
			if (Rule.Scope != SchedulingScope.Unplanned)
				uniqueKey = String.Format("ConfigurationBaseName:{0},SelectedDay:{1},SelectedHour:{2},RuleScope:{3},TimeToRun:{4},ProfileID:{5},ConfigurationName{6}", Configuration.BaseConfiguration.Name, SelectedDay, SelectedHour, Rule.Scope, TimeToRun, Profile.ID, Configuration.Name);
			else				
				uniqueKey = Guid.ToString();			
			return uniqueKey;
		}
		public override int GetHashCode()
		{
			int returnType = this.ToString().GetHashCode();
			return returnType;
		}
		public override bool Equals(object obj)
		{
			if ((object)obj == null)
				return false; 
			if (obj is SchedulingData)
				return obj.GetHashCode() == this.GetHashCode();
			else
				return false;
		}
		public static bool operator ==(SchedulingData sd1, SchedulingData sd2)
		{			
			return sd1.Equals(sd2);
		}
		public static bool operator !=(SchedulingData sd1, SchedulingData sd2)
		{
			return !sd1.Equals(sd2);
		}
		public static bool IsNull(SchedulingData obj)
		{
			if ((object)obj == null)
				return true;
			else
				return false;
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
