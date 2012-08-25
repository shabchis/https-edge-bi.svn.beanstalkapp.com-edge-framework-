using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Core.Services
{
	public class ServiceEnvironment
	{
		private Dictionary<string, IServiceExecutionHost> _hosts;

		public EventHandler<ServiceInstanceEventArgs> ServiceScheduleRequested;

		// TODO: remove the reference to proxy from here, this should be loaded from a list of hosts in the database
		//internal ServiceEnvironment(ServiceEnvironmentConfiguration)
		internal ServiceEnvironment(params IServiceExecutionHost[] hosts)
		{
			_hosts = new Dictionary<string,IServiceExecutionHost>();
			
			// TEMP till environment has DB list of hosts
			if (hosts.Length == 0)
				hosts = new IServiceExecutionHost[] { ServiceExecutionHost.LOCAL };

			foreach (IServiceExecutionHost host in hosts)
				_hosts.Add(host.HostName, host);
		}

		public ServiceInstance NewServiceInstance(ServiceConfiguration configuration)
		{
			return NewServiceInstance(configuration, null);
		}

		internal ServiceInstance NewServiceInstance(ServiceConfiguration configuration, ServiceInstance parent)
		{
			return new ServiceInstance(configuration, this, parent);
		}

		internal ServiceConnection AcquireHostConnection(string hostName, Guid instanceID)
		{
			// TODO: determine which host to connect to

			return new ServiceConnection(_hosts[hostName], instanceID);
		}

		public ServiceInstance GetServiceInstance(Guid instanceID)
		{
			throw new NotImplementedException();
		}

		public void Schedule(ServiceInstance instance)
		{
			throw new NotImplementedException();
		}
	}

	public class ServiceInstanceEventArgs : EventArgs
	{
		public ServiceInstance ServiceInstance { get; set; }
	}
}
