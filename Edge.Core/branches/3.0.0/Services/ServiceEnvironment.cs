using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Remoting;

namespace Edge.Core.Services
{
	public class ServiceEnvironment
	{
		private Dictionary<string, IServiceExecutionHost> _hosts;

		public event EventHandler<ServiceInstanceEventArgs> ServiceScheduleRequested;

		// TODO: remove the reference to proxy from here, this should be loaded from a list of hosts in the database
		//internal ServiceEnvironment(ServiceEnvironmentConfiguration)
		internal ServiceEnvironment(params IServiceExecutionHost[] hosts)
		{
			_hosts = new Dictionary<string,IServiceExecutionHost>();

			//if (hosts.Length == 0)
				//hosts = new IServiceExecutionHost[] { ServiceExecutionHost.LOCAL };

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

			var connection = new ServiceConnection(_hosts[hostName], instanceID);
			_hosts[hostName].OpenConnection(instanceID, connection.Guid, connection);
			return connection;
		}

		public ServiceInstance GetServiceInstance(Guid instanceID)
		{
			throw new NotImplementedException();
		}

		public void ScheduleService(ServiceInstance instance)
		{
			// TODO: temporarily using host to get to the target environment
			var host = (ServiceExecutionHost)_hosts[ServiceExecutionHost.LOCALNAME];
			if (RemotingServices.IsTransparentProxy(host))
			{
				host.InternalScheduleService(instance);
			}
			else
			{
				if (ServiceScheduleRequested != null)
					ServiceScheduleRequested(this, new ServiceInstanceEventArgs() { ServiceInstance = instance });
			}
		}
	}

	public class ServiceInstanceEventArgs : EventArgs
	{
		public ServiceInstance ServiceInstance { get; set; }
	}
}
