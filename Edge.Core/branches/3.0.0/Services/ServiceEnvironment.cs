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
using Newtonsoft.Json;
using System.Runtime.Serialization;

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
				var command = new SqlCommand(env.SP_HostList, connection);
				command.CommandType = CommandType.StoredProcedure;
				connection.Open();
				using (SqlDataReader reader = command.ExecuteReader())
				{
					while (reader.Read())
					{
						var info = new ServiceExecutionHostInfo()
						{
							HostName = SqlUtility.ClrValue<string>(reader["HostName"]),
							HostGuid = SqlUtility.ClrValue<string, Guid>(reader["HostGuid"], rawGuid => Guid.Parse(rawGuid), Guid.Empty),
							EndpointName = SqlUtility.ClrValue<string>(reader["EndpointName"]),
							EndpointAddress = SqlUtility.ClrValue<string>(reader["EndpointAddress"])
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
				var command = new SqlCommand(env.SP_HostRegister, connection);
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
				var command = new SqlCommand(env.SP_HostUnregister, connection);
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

		public void ScheduleServiceByName(string serviceName, Guid? profileID = null, ServiceConfiguration configuration = null)
		{
			throw new NotImplementedException();
		}

		public void ResetUnendedServices()
		{
			var env = this.EnvironmentConfiguration;
			using (var connection = new SqlConnection(env.ConnectionString))
			{
				var command = new SqlCommand(env.SP_InstanceReset, connection);
				command.CommandType = CommandType.StoredProcedure;
				connection.Open();
				command.ExecuteNonQuery();
			}
		}


		internal void SaveServiceInstance(
			ServiceExecutionHost host,
			Guid instanceID,
			Guid parentInstanceID,
			ServiceConfiguration config,
			ServiceStateInfo stateInfo,
			SchedulingInfo schedulingInfo
			)
		{
			// The first time we save write the configuration to XML. Otherwise ignore.
			string serializedConfig = null;
			if (stateInfo.State == ServiceState.Initializing)
			{
				var stringWriter = new StringWriter();
				using (var writer = new XmlTextWriter(stringWriter))
					new NetDataContractSerializer().WriteObject(writer, config);
				serializedConfig = stringWriter.ToString();
			}

			var env = this.EnvironmentConfiguration;
			using (var connection = new SqlConnection(env.ConnectionString))
			{
				var command = new SqlCommand(env.SP_InstanceSave, connection);
				command.CommandType = CommandType.StoredProcedure;
				command.Parameters.AddWithValue("@instanceID", instanceID.ToString("N"));
				command.Parameters.AddWithValue("@parentInstanceID", SqlUtility.SqlValue(parentInstanceID, Guid.Empty, () => parentInstanceID.ToString("N")));
				command.Parameters.AddWithValue("@profileID", SqlUtility.SqlValue(config.Profile, () => config.Profile.ProfileID.ToString("N")));
				command.Parameters.AddWithValue("@serviceName", config.ServiceName);
				command.Parameters.AddWithValue("@hostName", host.HostName);
				command.Parameters.AddWithValue("@hostGuid", host.HostGuid.ToString("N"));
				command.Parameters.AddWithValue("@progress", stateInfo.Progress);
				command.Parameters.AddWithValue("@state", stateInfo.State);
				command.Parameters.AddWithValue("@outcome", stateInfo.Outcome);
				command.Parameters.AddWithValue("@timeInitialized", SqlUtility.SqlValue(stateInfo.TimeInitialized, DateTime.MinValue));
				command.Parameters.AddWithValue("@timeStarted", SqlUtility.SqlValue(stateInfo.TimeStarted, DateTime.MinValue));
				command.Parameters.AddWithValue("@timeEnded", SqlUtility.SqlValue(stateInfo.TimeEnded, DateTime.MinValue));
				command.Parameters.AddWithValue("@timeLastPaused", SqlUtility.SqlValue(stateInfo.TimeLastPaused, DateTime.MinValue));
				command.Parameters.AddWithValue("@timeLastResumed", SqlUtility.SqlValue(stateInfo.TimeLastResumed, DateTime.MinValue));
				command.Parameters.AddWithValue("@resumeCount", stateInfo.ResumeCount);
				command.Parameters.AddWithValue("@configuration", SqlUtility.SqlValue(serializedConfig));
				command.Parameters.AddWithValue("@Scheduling_Status", SqlUtility.SqlValue(schedulingInfo, () => schedulingInfo.SchedulingStatus));
				command.Parameters.AddWithValue("@Scheduling_Scope", SqlUtility.SqlValue(schedulingInfo, () => schedulingInfo.SchedulingScope));
				command.Parameters.AddWithValue("@Scheduling_MaxDeviationBefore", SqlUtility.SqlValue(schedulingInfo, () => schedulingInfo.MaxDeviationBefore));
				command.Parameters.AddWithValue("@Scheduling_MaxDeviationAfter", SqlUtility.SqlValue(schedulingInfo, () => schedulingInfo.MaxDeviationAfter));
				command.Parameters.AddWithValue("@Scheduling_RequestedTime", SqlUtility.SqlValue(schedulingInfo, () => SqlUtility.SqlValue(schedulingInfo.RequestedTime, DateTime.MinValue)));
				command.Parameters.AddWithValue("@Scheduling_ExpectedStartTime", SqlUtility.SqlValue(schedulingInfo, () => SqlUtility.SqlValue(schedulingInfo.ExpectedStartTime, DateTime.MinValue)));
				command.Parameters.AddWithValue("@Scheduling_ExpectedEndTime", SqlUtility.SqlValue(schedulingInfo, () => SqlUtility.SqlValue(schedulingInfo.ExpectedEndTime, DateTime.MinValue)));

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
	public class ServiceEnvironmentConfiguration : Lockable
	{
		public string ConnectionString;
		public string DefaultHostName;

		public string SP_HostList;
		public string SP_HostRegister;
		public string SP_HostUnregister;
		public string SP_InstanceSave;
		public string SP_InstanceReset;
	}

	public class ServiceInstanceEventArgs : EventArgs
	{
		public ServiceInstance ServiceInstance { get; set; }
	}
}
