using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Core.Configuration;


namespace Edge.Core.Scheduling.Objects
{
	public class ServiceConfiguration
	{
        private Guid _guid;
		public int ID;
		public ServiceConfiguration BaseConfiguration;			
		public string Name;
		public int MaxConcurrent;
		public int MaxCuncurrentPerProfile;
		public Profile SchedulingProfile;
		public List<SchedulingRule> SchedulingRules=new List<SchedulingRule>();
		public bool Scheduled = false;
		public TimeSpan AverageExecutionTime=new TimeSpan(0,30,0);
		public TimeSpan MaxExecutionTime = new TimeSpan(0,60, 0);
		public ActiveServiceElement LegacyConfiguration;
		public int priority;

        public ServiceConfiguration()
        {
            _guid = new Guid();
        }
        public override int GetHashCode()
        {
            return _guid.GetHashCode();
        }
       
		
	}


}
