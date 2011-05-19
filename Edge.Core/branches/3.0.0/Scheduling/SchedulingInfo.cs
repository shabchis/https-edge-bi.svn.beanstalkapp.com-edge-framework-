using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;

namespace Edge.Core.Services2.Scheduling
{
	[Serializable]
	public class SchedulingInfo
	{
		public ServiceInstance Instance { get; private set;}
		public SchedulingRule Rule { get; internal set; }
		public int SelectedDay { get; internal set; }
		public TimeSpan SelectedHour { get; internal set; }
		public DateTime RequestedTimeStart { get; internal set; }
		public DateTime PlannedTimeStart { get; internal set; }
		public DateTime PlannedTimeEnd { get; internal set; }
		public TimeSpan ActualDeviation { get; internal set; }

		internal SchedulingInfo(ServiceInstance targetInstance)
		{
			Instance = targetInstance;
		}

		public SchedulingInfo()
		{
		}

        public override string ToString()
        {
            string uniqueKey = string.Empty;
            /*
            // Hash code example:
            string s1, s2;
            s1 = "blah blah";
            s2 = "blah blah";

            s1.GetHashCode() == s2.GetHashCode(); // this is true!!
            */
            if (Rule.Scope != SchedulingScope.Unplanned)
                uniqueKey = String.Format("Service:{0}, SelectedDay:{1}, SelectedHour:{2}, RuleScope:{3}, TimeToRun:{4}, ProfileID:{5}", Instance.Configuration.BaseConfiguration.ServiceName, SelectedDay, SelectedHour, Rule.Scope, RequestedTimeStart, Instance.Configuration.Profile.ID);
            else
            {
				//uniqueKey = String.Format("Service:{0},SelectedDay:{1},SelectedHour:{2},RuleScope:{3},TimeToRun:{4},ProfileID:{5}{6}", Configuration.BaseConfiguration.Name, SelectedDay, SelectedHour, Rule.Scope, TimeToRun, profileID, Guid);
				uniqueKey = String.Format("Unplanned: {0}", Guid.ToString());
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
			if (obj is SchedulingInfo)
                return obj.GetHashCode() == this.GetHashCode();
            else
                return false;
        }

		public static bool operator ==(SchedulingInfo sd1, SchedulingInfo sd2)
        {
            return sd1.Equals(sd2);
        }

		public static bool operator !=(SchedulingInfo sd1, SchedulingInfo sd2)
        {
            return !sd1.Equals(sd2);
        }
	}
}
