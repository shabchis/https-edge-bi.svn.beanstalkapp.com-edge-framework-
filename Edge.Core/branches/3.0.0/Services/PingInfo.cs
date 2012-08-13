using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Core.Services
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
