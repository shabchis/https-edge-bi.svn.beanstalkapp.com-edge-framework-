using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Remoting.Messaging;

namespace Edge.Core.Services2
{
	public class ServiceExecutionHost : MarshalByRefObject, IServiceHost
	{
		Dictionary<Guid, Service> _services = new Dictionary<Guid, Service>();
		
		public ServiceExecutionHost()
		{
			// TODO: demand ServiceHostingPermission
		}

		[OneWay]
		void IServiceHost.Initialize(ServiceInstance instance)
		{
			// Check if instance ID exists
			lock (_services)
			{
				if (_services.ContainsKey(instance.InstanceID))
					throw new ServiceException(String.Format("Service instance '{0}' is already initialized.", instance.InstanceID));
			}

			// Load the app domain
			AppDomain domain = AppDomain.CreateDomain(instance.ToString());

			domain.UnhandledException += new UnhandledExceptionEventHandler(DomainUnhandledException);
			domain.DomainUnload += new EventHandler(DomainUnload);

			// Instantiate the service type in the new domain
			Service serviceRef;
			if (instance.Configuration.AssemblyPath == null)
			{
				// No assembly path specified, most likely a core service
				Type serviceType = Type.GetType(instance.Configuration.ServiceType, false);
				if (serviceType == null)
					throw new ServiceException(String.Format("Service type '{0}' could not be found. Please specify AssemblyPath if the service is not in the host directory.", instance.Configuration.ServiceType));

				serviceRef = (Service)domain.CreateInstanceAndUnwrap(serviceType.Assembly.FullName, serviceType.FullName);
			}
			else
			{
				// A 3rd party service
				serviceRef = (Service)domain.CreateInstanceFromAndUnwrap(
					instance.Configuration.AssemblyPath,
					instance.Configuration.ServiceType
				);
			}

			// Host it
			lock (_services)
			{
				_services.Add(instance.InstanceID, serviceRef);
			}
	
			// Give the service ref its properties
			serviceRef.Init(this, instance);
		}

		[OneWay]
		void IServiceHost.Start(Guid instanceID)
		{
			Get(instanceID).Start();
		}

		[OneWay]
		void IServiceHost.Abort(Guid instanceID)
		{
			Get(instanceID).Abort();
		}

		[OneWay]
		internal void Unload(Guid instanceID)
		{
		}

		void DomainUnload(object sender, EventArgs e)
		{
			throw new NotImplementedException();
		}

		void DomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			throw new NotImplementedException();
		}

		Service Get(Guid instanceID)
		{
			Service service;
			if (!_services.TryGetValue(instanceID, out service))
				throw new ServiceException(String.Format("Service instance with ID {0} could not be found in this host.", instanceID));
			return service;
		}
	}

	internal interface IServiceHost
	{
		void Initialize(ServiceInstance instance);
		void Start(Guid instanceID);
		void Abort(Guid instanceID);
	}
}
