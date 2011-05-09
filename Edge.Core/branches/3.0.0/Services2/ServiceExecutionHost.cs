using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Core.Services2
{
	public class ServiceExecutionHost
	{
		public ServiceExecutionHost()
		{
		}

		public ServiceInstance NewServiceInstance(ServiceConfiguration config, ServiceInstance parentInstance)
		{
			ServiceInstance instance = new ServiceInstance()
			{
				InstanceID = Guid.NewGuid(),
				Configuration = config,
				Context = parentInstance != null ? parentInstance.Context : new ServiceExecutionContext()
			};

			return instance;
		}

		internal void InitializeService(ServiceInstance instance)
		{
			instance.State = Services.ServiceState.Initializing;

			AppDomain domain = AppDomain.CreateDomain(instance.ToString());
			instance.AttachTo((Service)domain.CreateInstanceAndUnwrap(typeof(Service).Assembly.FullName, typeof(Service).FullName));

			instance.State = Services.ServiceState.Ready;
		}

		public ServiceInstance GetServiceInstance(Guid instanceID)
		{
			throw new NotImplementedException();
		}
	}

	class program
	{
		static void Main()
		{
			ServiceExecutionHost host = new ServiceExecutionHost();

			ServiceInstance instance = host.NewServiceInstance(config,null);
			instance.Start();
		}
	}
}
