using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Core.Services;

namespace Edge.Core.Scheduling
{
	public struct PingInfo
	{
		public Guid InstanceGuid;
		public bool FromEngine;
		public DateTime Timestamp;
		public ServiceState State;
		public double Progress;
		public Exception Exception;
	}
}
