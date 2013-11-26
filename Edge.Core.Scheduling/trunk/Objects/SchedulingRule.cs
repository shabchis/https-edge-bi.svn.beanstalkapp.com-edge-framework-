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
        public List<int> Days { get; set; }// { get; }
        public List<TimeSpan> Hours { get; set; }// { get; }
        //public TimeSpan Frequency { get; set; }
        public TimeSpan MaxDeviationBefore { get; set; }
        public TimeSpan MaxDeviationAfter { get; set; }
        public DateTime SpecificDateTime { get; set; }
        // public Dictionary<string,object> ServiceSettings { get; }
    }
    public class SchedulingData
    {
        public Guid Guid;
        public ServiceConfiguration Configuration;
        public SchedulingRule Rule;
        public int profileID;
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
          
            if (Rule.Scope != SchedulingScope.UnPlanned)
                uniqueKey = String.Format("{0},{1},{2},{3},{4},{5},{6}", Configuration.BaseConfiguration.Name, SelectedDay, SelectedHour, Rule.Scope, TimeToRun, profileID,Configuration.Name);
            else
            {               
                uniqueKey = Guid.ToString();
            }
            return uniqueKey;
        }

        public override int GetHashCode()
        {
            int returnType = this.ToString().GetHashCode();
            return returnType;
        }

        public override bool Equals(object obj)
        {
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
    }
    public enum SchedulingScope
    {
        Day,
        Week,
        Month,
        UnPlanned
    }
}
