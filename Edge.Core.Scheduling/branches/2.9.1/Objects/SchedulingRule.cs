using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Core.Configuration;

namespace Edge.Core.Scheduling.Objects
{
	public class SchedulingRule
	{
		public Guid GuidForUnplaned;
		public SchedulingScope Scope { get; set; }
		public List<int> Days { get; set; }
		public List<TimeSpan> Times { get; set; }		
		public TimeSpan MaxDeviationBefore { get; set; }
		public TimeSpan MaxDeviationAfter { get; set; }
		public DateTime SpecificDateTime { get; set; }		
	}
	public class SchedulingData
	{
		internal Guid Guid;
		public ServiceConfiguration Configuration;
		public SchedulingRule Rule;
		public int ProfileID;
		public int SelectedDay;
		public TimeSpan SelectedHour;
		public DateTime TimeToRun;
		public ActiveServiceElement LegacyConfiguration;
		public int Priority;
		public SchedulingData()
		{
			Guid = Guid.NewGuid();
		}
		public override string ToString()
		{
			string uniqueKey = string.Empty;			
			if (Rule.Scope != SchedulingScope.Unplanned)
				uniqueKey = String.Format("ConfigurationBaseName:{0},SelectedDay:{1},SelectedHour:{2},RuleScope:{3},TimeToRun:{4},ProfileID:{5},ConfigurationName{6}", Configuration.BaseConfiguration.Name, SelectedDay, SelectedHour, Rule.Scope, TimeToRun, ProfileID, Configuration.Name);
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
