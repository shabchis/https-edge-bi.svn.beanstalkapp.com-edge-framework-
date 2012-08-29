using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Remoting;
using System.Data;
using System.Data.SqlClient;
using Edge.Core.Utilities;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

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
			if (_hosts == null)
				_hosts = new Dictionary<string, ServiceExecutionHostInfo>();
			else
				_hosts.Clear();

			var env = this.EnvironmentConfiguration;
			using (var connection = new SqlConnection(env.ConnectionString))
			{
				var command = new SqlCommand(env.HostListSP, connection);
				command.CommandType = CommandType.StoredProcedure;
				connection.Open();
				using (SqlDataReader reader = command.ExecuteReader())
				{
					while (reader.Read())
					{
						var info = new ServiceExecutionHostInfo()
						{
							HostName = (string) reader["HostName"],
							HostGuid = (Guid) reader["HostGuid"],
							EndpointName = (string)reader["EndpointName"],
							EndpointAddress = (string)reader["EndpointAddress"]
						};
						_hosts.Add(info.HostName, info);
					}
				}
			}
		}

		internal void RegisterHost(ServiceExecutionHost host)
		{
			var env = this.EnvironmentConfiguration;
			using (var connection = new SqlConnection(env.ConnectionString))
			{
				// FUTURE: in the future save all endpoints to DB
				var command = new SqlCommand(env.HostRegisterSP, connection);
				command.CommandType = CommandType.StoredProcedure;
				command.Parameters.AddWithValue("@hostName", host.HostName);
				command.Parameters.AddWithValue("@hostGuid", host.HostGuid.ToString("N"));
				command.Parameters.AddWithValue("@endpointName", host.WcfHost.Description.Endpoints.First(endpoint => endpoint.Name == "Default").Name);
				command.Parameters.AddWithValue("@endpointAddress", host.WcfHost.Description.Endpoints.First(endpoint => endpoint.Name == "Default").Address.ToString());
				connection.Open();
				command.ExecuteNonQuery();
			}
		}

		internal void UnregisterHost(ServiceExecutionHost host)
		{
			var env = this.EnvironmentConfiguration;
			using (var connection = new SqlConnection(env.ConnectionString))
			{
				var command = new SqlCommand(env.HostUnregisterSP, connection);
				command.CommandType = CommandType.StoredProcedure;
				command.Parameters.AddWithValue("@hostName", host.HostName);
				connection.Open();
				command.ExecuteNonQuery();
			}
		}

		internal ServiceConnection AcquireHostConnection(string hostName, Guid instanceID)
		{
			// FUTURE: determine which host to connect to on the fly, instead of getting hostName parameter

			ServiceExecutionHostInfo info;
			if (!_hosts.TryGetValue(hostName, out info))
				throw new ArgumentException(String.Format("Host '{0}' was not found. Try calling RefreshHosts if the host has been recently started.", hostName), "hostName");

			var connection = new ServiceConnection(this, instanceID, info.EndpointName, info.EndpointAddress);
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

		
		public ServiceInstance GetServiceInstance(Guid instanceID, string hostName = null)
		{
			throw new NotImplementedException();
		}

		public void ScheduleService(ServiceInstance instance)
		{
			throw new NotImplementedException();
			/*
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
			*/
		}

		public void ResetUnendedServices()
		{
			throw new NotImplementedException();
		}


		internal void SaveServiceInstance(ServiceExecutionHost host, ServiceInstance instance)
		{
			// The first time we save write the configuration to XML. Otherwise ignore.
			string configXml = null;
			if (instance.State == ServiceState.Initializing)
			{
				var stringWriter = new StringWriter();
				using (var writer = new XmlTextWriter(stringWriter))
					new XmlSerializer(instance.Configuration.GetType()).Serialize(writer, instance.Configuration);
				configXml = stringWriter.ToString();
			}

			var env = this.EnvironmentConfiguration;
			using (var connection = new SqlConnection(env.ConnectionString))
			{
				var command = new SqlCommand(env.InstanceSaveSP, connection);
				command.CommandType = CommandType.StoredProcedure;
				command.Parameters.AddWithValue("@instanceID", instance.InstanceID);
				command.Parameters.AddWithValue("@parentInstanceID", SqlUtility.NullIf(instance.ParentInstance, instance.ParentInstance.InstanceID));
				command.Parameters.AddWithValue("@profileID", SqlUtility.NullIf(instance.Configuration.Profile, instance.Configuration.Profile.ProfileID));
				command.Parameters.AddWithValue("@hostName", host.HostName);
				command.Parameters.AddWithValue("@hostGuid", host.HostGuid);
				command.Parameters.AddWithValue("@progress", instance.Progress);
				command.Parameters.AddWithValue("@state", instance.State);
				command.Parameters.AddWithValue("@outcome", instance.Outcome);
				command.Parameters.AddWithValue("@timeInitialized", instance.TimeInitialized);
				command.Parameters.AddWithValue("@timeStarted", instance.TimeStarted);
				command.Parameters.AddWithValue("@timeEnded", instance.TimeEnded);
				command.Parameters.AddWithValue("@timeLastPaused", instance.TimeLastPaused);
				command.Parameters.AddWithValue("@timeLastResumed", instance.TimeLastResumed);
				command.Parameters.AddWithValue("@resumeCount", instance.StateInfo.ResumeCount);
				command.Parameters.AddWithValue("@configuration", configXml);
				command.Parameters.AddWithValue("@Scheduling_Status", instance.SchedulingInfo.SchedulingStatus);
				command.Parameters.AddWithValue("@Scheduling_Scope", instance.SchedulingInfo.SchedulingScope);
				command.Parameters.AddWithValue("@Scheduling_MaxDeviationBefore", instance.SchedulingInfo.MaxDeviationBefore);
				command.Parameters.AddWithValue("@Scheduling_MaxDeviationAfter", instance.SchedulingInfo.MaxDeviationAfter);
				command.Parameters.AddWithValue("@Scheduling_RequestedTime", instance.SchedulingInfo.RequestedTime);
				command.Parameters.AddWithValue("@Scheduling_ExpectedStartTime", instance.SchedulingInfo.ExpectedStartTime);
				command.Parameters.AddWithValue("@Scheduling_ExpectedEndTime", instance.SchedulingInfo.ExpectedEndTime);
				
				connection.Open();
				command.ExecuteNonQuery();
			}
		}
	}

	public class ServiceExecutionHostInfo
	{
		public string HostName;
		public Guid HostGuid;
		public string EndpointName;
		public string EndpointAddress;
	}

	[Serializable]
	public class ServiceEnvironmentConfiguration: Lockable
	{
		public string ConnectionString;
		public string HostListSP;
		public string HostRegisterSP;
		public string HostUnregisterSP;
		public string InstanceSaveSP;
	}

	public class ServiceInstanceEventArgs : EventArgs
	{
		public ServiceInstance ServiceInstance { get; set; }
	}
}
