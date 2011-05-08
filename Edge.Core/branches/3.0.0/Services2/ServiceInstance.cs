using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;

namespace Edge.Core.Services2
{
	public class ServiceInstance: IServiceInfo
	{
		internal Service ServiceRef { get; set; }

		public Guid InstanceID { get; private set; }
		public ServiceConfiguration Configuration { get; private set; }
		public ServiceExecutionContext Context { get; private set; }
		public ServiceInstance ParentInstance { get; private set; }
		public double Progress { get; private set; }
		public Edge.Core.Services.ServiceState State { get; private set; }
		public Edge.Core.Services.ServiceOutcome Outcome { get; private set; }
		public object Output { get; private set; }
		public SchedulingData SchedulingData { get; private set; }
		public ReadOnlyObservableCollection<ServiceInstance> ChildInstances { get; private set; }

		public override string ToString()
		{
			return String.Format("{0} (profile: {1}, guid: {2})",
				Configuration.ServiceName,
				Configuration.Profile == null ? "default" : Configuration.Profile.Name,
				InstanceID
			);
		}
	}
}
