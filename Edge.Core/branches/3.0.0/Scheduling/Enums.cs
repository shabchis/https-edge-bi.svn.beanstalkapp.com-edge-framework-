using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Core.Services
{
	public enum SchedulingStatus
	{
		New = 0,
		WaitingForSchedule = 1,
		Scheduled = 2,
		Activated = 3,
		CouldNotBeScheduled = 4
	}
}
