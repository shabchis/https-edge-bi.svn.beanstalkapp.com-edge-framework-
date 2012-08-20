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
		public TimeSpan MaxDeviationAfter { get; set; }
		public TimeSpan MaxDeviationBefore { get; set; }
		public TimeSpan ActualDeviation 
		{
			get
			{
				return ExpectedStartTime.Subtract(RequestedTime);
			}
		}
		public SchedulingStatus SchedulingStatus;

		public SchedulingScope Scope { get; set; }
	}
}
