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
		ServiceEnvironment Environment { get; }
		ServiceExecutionContext Context { get; }
		ServiceInstance ParentInstance { get; }
		double Progress { get; }
		ServiceState State { get; }
		ServiceOutcome Outcome { get; }
		object Output { get; }
		SchedulingInfo SchedulingInfo { get; }
		//ReadOnlyObservableCollection<ServiceInstance> ChildInstances { get; }
	}
}
