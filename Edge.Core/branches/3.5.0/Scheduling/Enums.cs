using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Core.Services
{
	public enum SchedulingStatus
	{
		New = 0,
		Scheduled = 1,
		Activated = 2,
		Expired = 7,
		Canceled = 8
	}
}
