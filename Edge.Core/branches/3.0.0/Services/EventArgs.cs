using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Core.Services
{
	[Serializable]
	public class ServiceOutputEventArgs : EventArgs
	{
		public object Output { get; set; }

		public ServiceOutputEventArgs(object output)
		{
			this.Output = output;
		}
	}

	[Serializable]
	public class ServiceInstanceEventArgs : EventArgs
	{
		public ServiceInstance ServiceInstance { get; set; }
	}

	[Serializable]
	public class ScheduleUpdatedEventArgs : EventArgs
	{
		// TODO: add schedule updates data
	}
}
