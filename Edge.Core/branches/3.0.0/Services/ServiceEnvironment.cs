using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Core.Services
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

		public ServiceInstance NewService(ServiceConfiguration configuration)
		{
			return NewService(configuration, null);
		}

		internal ServiceInstance NewService(ServiceConfiguration configuration, Service parent)
		{
			return new ServiceInstance(this, configuration, parent);
		}

		public void ScheduleService(ServiceInstance instance)
		{
			//ServiceConfiguration config;
			//config.GetBaseConfiguration(ServiceConfigurationLevel.Profile) == config.GetBaseConfiguration(ServiceConfigurationLevel.Profile)
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
