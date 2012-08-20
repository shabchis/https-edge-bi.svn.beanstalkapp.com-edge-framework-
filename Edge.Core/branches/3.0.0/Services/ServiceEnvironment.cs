using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Core.Services
{
	public class ServiceEnvironment
	{
		private IServiceHost _host;

		// TODO: remove the reference to proxy from here, this should be loaded from a list of hosts in the database
		internal ServiceEnvironment(IServiceHost proxy)
		{
			_host = proxy;
		}

		public ServiceInstance NewServiceInstance(ServiceConfiguration configuration)
		{
			return NewServiceInstance(configuration, null);
		}

		internal ServiceInstance NewServiceInstance(ServiceConfiguration configuration, Service parent)
		{
			return new ServiceInstance(this, configuration, parent);
		}

		internal IServiceConnection AcquireHostConnection(ServiceInstance instance)
		{
			// TODO: determine which host to connect to
			return new LocalServiceConnection(_host, instance.InstanceID);
		}

		public ServiceInstance GetServiceInstance(Guid instanceID)
		{
			throw new NotImplementedException();
		}
	}
}
