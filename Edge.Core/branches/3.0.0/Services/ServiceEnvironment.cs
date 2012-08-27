using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Remoting;
using System.Data;
using System.Data.SqlClient;

namespace Edge.Core.Services
{
	public class ServiceEnvironment
	{
		private Dictionary<string, ServiceExecutionHostInfo> _hosts;

		public ServiceEnvironmentConfiguration EnvironmentConfiguration { get; private set; }

		public event EventHandler<ServiceInstanceEventArgs> ServiceScheduleRequested;

		// TODO: remove the reference to proxy from here, this should be loaded from a list of hosts in the database
		public ServiceEnvironment(ServiceEnvironmentConfiguration environmentConfig)
		{
			this.EnvironmentConfiguration = environmentConfig;
			RefreshHosts();
		}

		public void RefreshHosts()
		{
			var env = this.EnvironmentConfiguration;
			using (var connection = new SqlConnection(env.ConnectionString))
			{
				var command = new SqlCommand(env.HostListSP, connection);
				command.CommandType = CommandType.StoredProcedure;
				connection.Open();
				using (SqlDataReader reader = command.ExecuteReader())
				{
					var info = new ServiceExecutionHostInfo()
					{
						HostName = reader["HostName"] as string
					};
					_hosts.Add(info.HostName, info);
				}
			}
		}

		internal void RegisterHost(ServiceExecutionHost host)
		{
			var env = this.EnvironmentConfiguration;
			using (var connection = new SqlConnection(env.ConnectionString))
			{
				var command = new SqlCommand(env.HostRegisterSP, connection);
				command.CommandType = CommandType.StoredProcedure;
				command.Parameters.AddWithValue("@hostName", host.HostName);
				command.Parameters.AddWithValue("@binding", host.W
				connection.Open();
			}
		}

		internal ServiceConnection AcquireHostConnection(string hostName, Guid instanceID)
		{
			// FUTURE: determine which host to connect to on the fly, instead of getting hostName parameter

			ServiceExecutionHostInfo info;
			if (!_hosts.TryGetValue(hostName, out info))
				throw new ArgumentException(String.Format("Host '{0}' was not found. Try calling RefreshHosts if the host has been recently started.", hostName), "hostName");

			var connection = new ServiceConnection(instanceID);
			return connection;
		}


		public ServiceInstance NewServiceInstance(ServiceConfiguration configuration)
		{
			return NewServiceInstance(configuration, null);
		}

		internal ServiceInstance NewServiceInstance(ServiceConfiguration configuration, ServiceInstance parent)
		{
			return new ServiceInstance(configuration, this, parent);
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

	public class ServiceExecutionHostInfo
	{
		public string HostName;
	}

	public class ServiceEnvironmentConfiguration: Lockable
	{
		public string ConnectionString;
		public string HostListSP;
		public string HostRegisterSP;
	}

	public class ServiceInstanceEventArgs : EventArgs
	{
		public ServiceInstance ServiceInstance { get; set; }
	}
}
