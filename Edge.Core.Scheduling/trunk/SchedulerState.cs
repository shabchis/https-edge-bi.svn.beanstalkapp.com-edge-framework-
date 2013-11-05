using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Core.Scheduling
{
	public enum SchedulerState
	{
		Stopped = 0,
		Starting = 1,
		Started = 2,
		Stopping = 3
	}
}
