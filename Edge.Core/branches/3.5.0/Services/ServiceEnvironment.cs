using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Core
{
	public class ServiceEnvironment
	{
		private IServiceHost _host;

		public ServiceEnvironment()
		{
			_host = new ServiceExecutionHost();
		}

		internal ServiceEnvironment(IServiceHost proxy)
		{
			_host = proxy;
		}

		public ServiceInstance CreateServiceInstance(ServiceConfiguration configuration)
		{
			return CreateServiceInstance(configuration, null);
		}

		internal ServiceInstance CreateServiceInstance(ServiceConfiguration configuration, ServiceInstance parentInstance)
		{
			// TODO: demand ServiceExecutionPermission
			return new ServiceInstance(this, configuration, parentInstance);
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
