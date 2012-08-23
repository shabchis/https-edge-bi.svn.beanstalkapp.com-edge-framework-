using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Core.Services
{
	public class ServiceEnvironment
	{
		private Dictionary<string, IServiceExecutionHost> _hosts;

		public EventHandler ServiceScheduleRequested;

		// TODO: remove the reference to proxy from here, this should be loaded from a list of hosts in the database
		internal ServiceEnvironment(params IServiceExecutionHost[] hosts)
		{
			_hosts = new Dictionary<string,IServiceExecutionHost>();
			foreach (IServiceExecutionHost host in hosts)
				_hosts.Add(host.HostName, host);
		}

		public ServiceInstance NewServiceInstance(ServiceConfiguration configuration)
		{
			return NewServiceInstance(configuration, null);
		}

		internal ServiceInstance NewServiceInstance(ServiceConfiguration configuration, Service parent)
		{
			return new ServiceInstance(configuration, this, parent);
		}

		internal ServiceConnection AcquireHostConnection(Guid instanceID)
		{
			// TODO: determine which host to connect to

			return new ServiceConnection(_host, instanceID);
		}

		public ServiceInstance GetServiceInstance(Guid instanceID)
		{
			throw new NotImplementedException();
		}

		public void Schedule(ServiceInstance instance)
		{
		}
	}
}
