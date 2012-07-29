using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Core.Scheduling.Objects
{
	public class SchedulingRequest
	{
		public Guid RequestID { get; private set; }
		public ServiceConfiguration Configuration { get;  set; }
		public SchedulingRule Rule { get; private set; }
		public DateTime RequestedTime { get; private set; }
		public SchedulingRequest(ServiceConfiguration configuration, SchedulingRule rule, DateTime requestedTime)
		{
			this.Configuration = configuration;
			this.Rule = rule;
			this.RequestedTime = requestedTime;

			if (rule.Scope == SchedulingScope.Unplanned)
				this.RequestID = rule.GuidForUnplanned;
			else
				this.RequestID = Guid.NewGuid();
		}
		public ServiceInstance Instance
		{
			get
			{
				if (this.Configuration is ServiceInstanceConfiguration)
					return ((ServiceInstanceConfiguration)Configuration).Instance;
				else
					return null;
			}
		}
		public string UniqueKey
		{
			get
			{
				return String.Format("profile:{0},base:{1},name:{2},scope:{3},time:{4}", this.Configuration.Profile.ID, Configuration.BaseConfiguration.Name, Configuration.Name, Rule.Scope, RequestedTime);
			}
		}

		/*
		public override string ToString()
		{
			string uniqueKey;
			if (Rule.Scope != SchedulingScope.Unplanned)
				uniqueKey = String.Format("profile:{0},base:{1},name:{2},scope:{3},time:{4}", this.Configuration.Profile.ID, Configuration.BaseConfiguration.Name, Configuration.Name, Rule.Scope, RequestedTime);
			else
				uniqueKey = Rule.GuidForUnplanned.ToString() ;

			return uniqueKey;
		}

		public override int GetHashCode()
		{
			int returnType = Rule.Scope != SchedulingScope.Unplanned ?
				this.ToString().GetHashCode() :
				Rule.GuidForUnplanned.GetHashCode();
			return returnType;
		}

		public override bool Equals(object obj)
		{
			if ((object)obj == null)
				return false;
			if (obj is SchedulingRequest)
				return obj.GetHashCode() == this.GetHashCode();
			else
				return false;
		}

		public static bool operator ==(SchedulingRequest sd1, SchedulingRequest sd2)
		{
			return sd1.Equals(sd2);
		}

		public static bool operator !=(SchedulingRequest sd1, SchedulingRequest sd2)
		{
			return !sd1.Equals(sd2);
		}
		*/
	}
}
