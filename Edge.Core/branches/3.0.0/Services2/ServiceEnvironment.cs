﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Core.Services2
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

		public ServiceInstance CreateServiceInstance(ServiceConfiguration configuration, ServiceInstance parentInstance)
		{
			// TODO: demand ServiceExecutionPermission
			return new ServiceInstance(this, configuration, parentInstance);
		}

		internal IServiceConnection AcquireHost(ServiceInstance instance)
		{
			
			// TODO: determine which host to connect to
			return new LocalServiceConnection(_host);
		}

		public ServiceInstance GetServiceInstance(Guid instanceID)
		{
			throw new NotImplementedException();
		}
	}
}
