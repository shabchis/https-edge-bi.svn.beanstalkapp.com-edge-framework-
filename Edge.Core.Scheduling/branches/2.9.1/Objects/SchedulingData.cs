using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Core.Scheduling.Objects
{
	public class SchedulingRequest
	{
		public Guid Guid;
		public ServiceConfiguration Configuration;
		public SchedulingRule Rule;
		public DateTime RequestedTime;

		public SchedulingRequest()
		{
			Guid = Guid.NewGuid();
		}

		public override string ToString()
		{
			string uniqueKey;
			if (Rule.Scope != SchedulingScope.Unplanned)
				uniqueKey = String.Format("profile:{0},base:{1},name:{2},scope:{3},time:{4}", this.Configuration.Profile.ID, Configuration.BaseConfiguration.Name, Configuration.Name, Rule.Scope, RequestedTime);
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
	}
}
