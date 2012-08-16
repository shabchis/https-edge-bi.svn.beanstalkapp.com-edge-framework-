using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Core.Services;

namespace Edge.Core.Services
{
	[Serializable]
	public struct SchedulingInfo
	{
		public DateTime RequestedTime;
		public DateTime ExpectedStartTime;
		public DateTime ExpectedEndTime;
		public SchedulingStatus SchedulingStatus;
	}
}
