using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;

namespace Edge.Core.Services
{
	public interface IServiceInfo
	{
		Guid InstanceID { get; }
		ServiceConfiguration Configuration { get; }
		ServiceEnvironment Environment { get; }
		ServiceInstance ParentInstance { get; }
		double Progress { get; }
		ServiceState State { get; }
		ServiceOutcome Outcome { get; }
		object Output { get; }
		SchedulingInfo SchedulingInfo { get; }
		DateTime TimeInitialized { get; }
		DateTime TimeStarted { get; }
		DateTime TimeEnded { get; }
		//ReadOnlyObservableCollection<ServiceInstance> ChildInstances { get; }
	}
}
