using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Core.Configuration;
using Legacy = Edge.Core.Services; 


namespace Edge.Core.Scheduling.Objects
{
	public class ServiceConfiguration
	{
		private int _maxConcurrent = 1;
		private int _maxCuncurrentPerProfile = 1;
        private Guid _guid;
		public int ID;
		public ServiceConfiguration BaseConfiguration;			
		public string Name;
		public int MaxConcurrent
		{
			get
			{
				return _maxConcurrent;
			}
			set
			{
				if (value != 0)
					_maxConcurrent = value;
				else
					value = 999;
			}
		}
		public int MaxCuncurrentPerProfile
		{
			get
			{
				return _maxCuncurrentPerProfile;
			}
			set
			{
				if (value != 0)
					_maxCuncurrentPerProfile = value;
				else
					value = 999;
			}
		}
		public Profile SchedulingProfile;
		public List<SchedulingRule> SchedulingRules=new List<SchedulingRule>();
		public bool Scheduled = false;
		public TimeSpan AverageExecutionTime=new TimeSpan(0,30,0);
		public TimeSpan MaxExecutionTime = new TimeSpan(0,60, 0);
		public ActiveServiceElement LegacyConfiguration;
		public int priority;
		public Legacy.ServiceInstance Instance = null;
		

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
