using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;

namespace Edge.Core.Services2
{
	public interface IServiceView
	{
		Guid InstanceID { get; }
		ServiceConfiguration Configuration { get; }
		ServiceExecutionContext Context { get; }
		ServiceInstance ParentInstance { get; }
		double Progress { get; }
		Edge.Core.Services.ServiceState State { get; }
		Edge.Core.Services.ServiceOutcome Outcome { get; }
		object Output { get; }
		SchedulingData SchedulingData { get; }
		ReadOnlyObservableCollection<ServiceInstance> ChildInstances { get; private set; }
	}
}
