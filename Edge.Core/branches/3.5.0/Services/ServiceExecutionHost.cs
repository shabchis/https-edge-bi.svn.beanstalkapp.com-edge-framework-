using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Remoting.Messaging;

namespace Edge.Core
{
	public class ServiceExecutionHost : MarshalByRefObject, IServiceHost
	{
		class ServiceRuntimeInfo
		{
			public readonly Guid InstanceID;
			public readonly Service ServiceRef;
			public readonly AppDomain AppDomain;

			public ServiceRuntimeInfo(Guid instanceID, Service serviceRef, AppDomain appDomain)
			{
				InstanceID = instanceID;
				ServiceRef = serviceRef;
				AppDomain = appDomain;
			}
		}

		public readonly Guid HostID;
		ServiceExecutionHost _innerHost = null;
		Dictionary<Guid, ServiceRuntimeInfo> _services = new Dictionary<Guid, ServiceRuntimeInfo>();
		Dictionary<int, Guid> _serviceByAppDomain = new Dictionary<int, Guid>();

		bool IsWrapped { get { return _innerHost != null; } }

		public ServiceExecutionHost(): this(true, Guid.NewGuid())
		{
			// TODO: demand ServiceHostingPermission
		}

		private ServiceExecutionHost(bool wrapped, Guid hostID)
		{
			this.HostID = hostID;

			if (!wrapped)
				return;

			AppDomain domain = AppDomain.CreateDomain(String.Format("Edge host ({0})", HostID));
			_innerHost = (ServiceExecutionHost) domain.CreateInstanceAndUnwrap
			(
				typeof(ServiceExecutionHost).Assembly.FullName,
				typeof(ServiceExecutionHost).FullName,
				false,
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.CreateInstance | System.Reflection.BindingFlags.Instance,
				null,
				new object[] { false, hostID }, // these are ctor arguments
				System.Globalization.CultureInfo.CurrentCulture,
				null
			);
		}

		[OneWay]
		void IServiceHost.Initialize(ServiceInstance instance)
		{
			if (this.IsWrapped)
			{
				((IServiceHost)_innerHost).Initialize(instance);
				return;
			}

			// Check if instance ID exists
			lock (_services)
			{
				if (_services.ContainsKey(instance.InstanceID))
					throw new ServiceException(String.Format("Service instance '{0}' is already initialized.", instance.InstanceID));
			}

			// Load the app domain, and attach to its events
			AppDomain domain = AppDomain.CreateDomain("Edge service - " + instance.ToString());
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

			// Connect the service to the instance
			serviceRef.Connect(instance.Connection);

			// Host it
			lock (_services)
			{
				var info = new ServiceRuntimeInfo(instance.InstanceID, serviceRef, domain);
				_services.Add(instance.InstanceID, info);
				_serviceByAppDomain.Add(domain.Id, info.InstanceID);
			}
	
			// Give the service ref its properties
			serviceRef.Init(this, instance);
		}

		[OneWay]
		void IServiceHost.Start(Guid instanceID)
		{
			if (this.IsWrapped)
			{
				((IServiceHost)_innerHost).Start(instanceID);
				return;
			}

			Get(instanceID).ServiceRef.Start();
		}

		/// <summary>
		/// Aborts execution of a running service.
		/// </summary>
		/// <param name="instanceID"></param>
		[OneWay]
		void IServiceHost.Abort(Guid instanceID)
		{
			if (this.IsWrapped)
			{
				((IServiceHost)_innerHost).Abort(instanceID);
				return;
			}

			Get(instanceID).ServiceRef.Abort();
		}

		/// <summary>
		/// Disconnects a connection from a running service instance.
		/// </summary>
		[OneWay]
		void IServiceHost.Disconnect(Guid instanceID, Guid connectionGuid)
		{
			if (this.IsWrapped)
			{
				((IServiceHost)_innerHost).Disconnect(instanceID, connectionGuid);
				return;
			}

			var info = Get(instanceID, false);
			
			if (info != null)
				info.ServiceRef.Disconnect(connectionGuid);
		}

		[OneWay]
		internal void Log(Guid instanceID, LogMessage message)
		{
			// TODO: do something with the log messages
		}

		void DomainUnload(object sender, EventArgs e)
		{
			// Get the service instance ID of the appdomain
			AppDomain appDomain = (AppDomain) sender;
			Guid instanceID;
			if (!_serviceByAppDomain.TryGetValue(appDomain.Id, out instanceID))
				return;

			// Get the runtime in the appdomain
			ServiceRuntimeInfo info = Get(instanceID, throwex: false);
			if (info == null)
				return;

			// Remove the service
			lock (_services)
			{
				_services.Remove(instanceID);
				_serviceByAppDomain.Remove(appDomain.Id);
			}
		}

		ServiceRuntimeInfo Get(Guid instanceID, bool throwex = true)
		{
			ServiceRuntimeInfo service;
			if (!_services.TryGetValue(instanceID, out service))
				if (throwex)
					throw new ServiceException(String.Format("Service instance with ID {0} could not be found in this host.", instanceID));
				else
					return null;
			return service;
		}
	}

	internal interface IServiceHost
	{
		void Initialize(ServiceInstance instance);
		void Start(Guid instanceID);
		void Abort(Guid instanceID);
		void Disconnect(Guid instanceID, Guid connectionID);
	}
}
