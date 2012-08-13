using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Core.Services
{
	/// <summary>
	/// 
	/// </summary>
	public class ServiceStateChangedEventArgs: EventArgs
	{
		public readonly ServiceState StateBefore;
		public readonly ServiceState StateAfter;

		public ServiceStateChangedEventArgs(ServiceState before, ServiceState after)
		{
			StateBefore = before;
			StateAfter = after;
		}
	}

	/// <summary>
	/// 
	/// </summary>
	public class ServiceRequestedEventArgs: EventArgs
	{
		public readonly ServiceInstance RequestedService;
		public readonly string ServiceName;
		public readonly int AccountID;

		/// <summary>
		/// The number of this attempt after a failure.
		/// </summary>
		public readonly int AttemptNumber;

		internal ServiceRequestedEventArgs(ServiceInstance service, int attemptNumber)
		{
			RequestedService = service;
			ServiceName = service.Configuration.Name;
			AccountID = service.AccountID;
			AttemptNumber = attemptNumber;
		}

		internal ServiceRequestedEventArgs(string serviceName, int accountID)
		{
			RequestedService = null;
			ServiceName = serviceName;
			AccountID = accountID;
			AttemptNumber = 1;
		}
	}
}
