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
using System.ServiceModel.Description;
using System.ServiceModel;

namespace Edge.Core.Services
{
	public class ServiceEnvironment : IServiceEnvironmentEventSender
	{
		private Dictionary<string, ServiceExecutionHostInfo> _hosts;

		public ServiceEnvironmentConfiguration EnvironmentConfiguration { get; private set; }

	    private ServiceEnvironment(ServiceEnvironmentConfiguration environmentConfig)
		{
			this.EnvironmentConfiguration = environmentConfig;
			RefreshHosts();
		}

		public static ServiceEnvironment Load(ServiceEnvironmentConfiguration environmentConfig)
		{
			return new ServiceEnvironment(environmentConfig);
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
				var command = new SqlCommand(env.SP_HostListGet, connection);
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
				command.Parameters.AddWithValue("@endpointName", host.WcfHost.Description.Endpoints.First(endpoint => endpoint.Name == typeof(ServiceExecutionHost).FullName).Name);
				command.Parameters.AddWithValue("@endpointAddress", host.WcfHost.Description.Endpoints.First(endpoint => endpoint.Name == typeof(ServiceExecutionHost).FullName).Address.ToString());
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

		public ServiceInstance GetServiceInstance(Guid instanceID, bool stateInfoOnly = false)
		{
			var env = this.EnvironmentConfiguration;
			using (var connection = new SqlConnection(env.ConnectionString))
			{
				var command = new SqlCommand(env.SP_InstanceGet, connection);
				command.CommandType = CommandType.StoredProcedure;
				command.Parameters.AddWithValue("@instanceID", instanceID.ToString("N"));
				command.Parameters.AddWithValue("@stateInfoOnly", stateInfoOnly);
				connection.Open();
				ServiceInstance requestedInstance = null;
				using (SqlDataReader reader = command.ExecuteReader())
				{
					ServiceInstance instance = null;
					ServiceInstance childInstance = null;
					do
					{
						if (!reader.Read())
							continue;

						instance = ServiceInstance.FromSqlData(reader, this, childInstance, stateInfoOnly);
						if (requestedInstance == null)
							requestedInstance = instance;

						childInstance = instance;
					}
					while (reader.NextResult());
				}
				return requestedInstance;
			}
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
		/// <summary>
		/// Get statistics for execution time per each service from DB
		/// </summary>
		/// <returns>dictionary: key = service name and profile ID, value = execution time</returns>
		public Dictionary<string, long> GetServiceExecutionStatistics(int percentile)
		{
			var statisticsDict = new Dictionary<string, long>();

			var env = this.EnvironmentConfiguration;
			using (var connection = new SqlConnection(env.ConnectionString))
			{
				var command = new SqlCommand(env.SP_ServicesExecutionStatistics, connection);
				command.CommandType = CommandType.StoredProcedure;
				command.Parameters.AddWithValue("@Percentile", percentile);
				connection.Open();

				using (SqlDataReader reader = command.ExecuteReader())
				{
					while (reader.Read())
					{
						var configID = SqlUtility.ClrValue<string>(reader["ConfigID"]);
						var profileID = SqlUtility.ClrValue<string>(reader["ProfileID"]);
						var key = String.Format("ConfigID:{0},ProfileID:{1}", configID, profileID);
						if (!statisticsDict.ContainsKey(key))
						{
							statisticsDict.Add(key, long.Parse(reader["Value"].ToString()));
						}
					}
				}
			}
			return statisticsDict;
		}

		/// <summary>
		/// Get all service instances from the DB
		/// </summary>
		/// <returns></returns>
		public List<ServiceInstance> GetServiceInstanceActiveList()
		{
			var instanceList = new List<ServiceInstance>();
			using (var connection = new SqlConnection(EnvironmentConfiguration.ConnectionString))
			{
				var command = new SqlCommand(EnvironmentConfiguration.SP_InstanceActiveListGet, connection);
				command.CommandType = CommandType.StoredProcedure;
				command.Parameters.AddWithValue("@timeframeStart", DateTime.Now);
				connection.Open();
				using (SqlDataReader reader = command.ExecuteReader())
				{
					while (reader.Read())
					{
						var instance = ServiceInstance.FromSqlData(reader, this, null, false);
						// all instances in DB are activated (scheduling status is not saved in DB)
						instance.SchedulingInfo.SchedulingStatus = SchedulingStatus.Activated;
						instanceList.Add(instance);
					}
				}
			}
			return instanceList;
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
				command.Parameters.AddWithValue("@Scheduling_MaxDeviationBefore", SqlUtility.SqlValue(schedulingInfo, () => schedulingInfo.MaxDeviationBefore.TotalSeconds));
                command.Parameters.AddWithValue("@Scheduling_MaxDeviationAfter", SqlUtility.SqlValue(schedulingInfo, () => schedulingInfo.MaxDeviationAfter.TotalSeconds));
				command.Parameters.AddWithValue("@Scheduling_RequestedTime", SqlUtility.SqlValue(schedulingInfo, () => SqlUtility.SqlValue(schedulingInfo.RequestedTime, DateTime.MinValue)));
				command.Parameters.AddWithValue("@Scheduling_ExpectedStartTime", SqlUtility.SqlValue(schedulingInfo, () => SqlUtility.SqlValue(schedulingInfo.ExpectedStartTime, DateTime.MinValue)));
				command.Parameters.AddWithValue("@Scheduling_ExpectedEndTime", SqlUtility.SqlValue(schedulingInfo, () => SqlUtility.SqlValue(schedulingInfo.ExpectedEndTime, DateTime.MinValue)));

				connection.Open();
				command.ExecuteNonQuery();
			}
		}

		// .............................................................
		// ENVIRONMENT EVENTS

        Dictionary<ServiceEnvironmentEventType, List<ServiceEnvironmentEventListenerInfo>> _environmentListeners = null;
	
		public void RefreshEventListenersList()
		{
			if (_environmentListeners == null)
			{
				_environmentListeners = new Dictionary<ServiceEnvironmentEventType, List<ServiceEnvironmentEventListenerInfo>>();
			}
			else
			{
				foreach (var list in _environmentListeners.Values)
					list.Clear();
			}

			var env = this.EnvironmentConfiguration;
			using (var connection = new SqlConnection(env.ConnectionString))
			{
				var command = new SqlCommand(env.SP_EnvironmentEventListenerListGet, connection);
				command.CommandType = CommandType.StoredProcedure;
				connection.Open();
				using (SqlDataReader reader = command.ExecuteReader())
				{
					while (reader.Read())
					{
						var info = new ServiceEnvironmentEventListenerInfo()
						{
							ListenerID = SqlUtility.ClrValue<string, Guid>(
								reader["ListenerID"],
								guidRaw => Guid.Parse(guidRaw),
								Guid.Empty
							),
							EventType = SqlUtility.ClrValue<string, ServiceEnvironmentEventType>(
								reader["EventType"],
								ev => (ServiceEnvironmentEventType) Enum.Parse(typeof(ServiceEnvironmentEventType), ev, false),
								(ServiceEnvironmentEventType) 0
							),
							EndpointName = SqlUtility.ClrValue<string>(reader["EndpointName"]),
							EndpointAddress = SqlUtility.ClrValue<string>(reader["EndpointAddress"])
						};
						List<ServiceEnvironmentEventListenerInfo> list;
						if (!_environmentListeners.TryGetValue(info.EventType, out list))
							_environmentListeners.Add(info.EventType, list = new List<ServiceEnvironmentEventListenerInfo>());

						list.Add(info);
					}
				}
			}
		}

		public ServiceEnvironmentEventListener ListenForEvents(params ServiceEnvironmentEventType[] eventTypes)
		{
			ServiceEnvironmentEventListener listener = new ServiceEnvironmentEventListener(this, eventTypes);
			RegisterEventListener(listener);
			return listener;
		}

		public void AddToSchedule(ServiceInstance instance)
		{
			SendEnvironmentEvent(ServiceEnvironmentEventType.ServiceRequiresScheduling,
				listener => listener.ServiceRequiresScheduling(new ServiceInstanceEventArgs() { ServiceInstance = instance })
			);
		}

		internal void RegisterEventListener(ServiceEnvironmentEventListener listener)
		{
			var env = this.EnvironmentConfiguration;
			using (var connection = new SqlConnection(env.ConnectionString))
			{
				connection.Open();
				using (SqlTransaction transaction = connection.BeginTransaction())
				{
					try
					{
						var command = new SqlCommand(env.SP_EnvironmentEventListenerRegister, connection, transaction)
						{
							CommandType = CommandType.StoredProcedure
						};

						foreach (ServiceEnvironmentEventType eventType in listener.EventTypes)
						{
							command.Parameters.AddWithValue("@listenerID", listener.ListenerID.ToString("N"));
							command.Parameters.AddWithValue("@eventType", eventType.ToString());
							command.Parameters.AddWithValue("@endpointName", listener.WcfHost.Description.Endpoints.First(endpoint => endpoint.Name == typeof(ServiceEnvironmentEventListener).FullName).Name);
							command.Parameters.AddWithValue("@endpointAddress", listener.WcfHost.Description.Endpoints.First(endpoint => endpoint.Name == typeof(ServiceEnvironmentEventListener).FullName).Address.ToString());

							command.ExecuteNonQuery();
						}

						transaction.Commit();
					}
					catch (Exception ex)
					{
						try { transaction.Rollback(); }
						catch { }

						listener.Close(false);

						throw new Edge.Core.Services.ServiceEnvironmentException("Could not register listener in environment database. The listener has been closed. See inner exception for details.", ex);
					}
				}
			}
		}

		internal void UnregisterEventListener(ServiceEnvironmentEventListener listener)
		{
			var env = this.EnvironmentConfiguration;
			using (var connection = new SqlConnection(env.ConnectionString))
			{
				connection.Open();
				var command = new SqlCommand(env.SP_EnvironmentEventListenerUnregister, connection)
				{
					CommandType = CommandType.StoredProcedure
				};
				command.Parameters.AddWithValue("@listenerID", listener.ListenerID.ToString("N"));
				command.ExecuteNonQuery();
			}
		}

		void SendEnvironmentEvent(ServiceEnvironmentEventType eventType, Action<IServiceEnvironmentEventListener> listenerAction)
		{
			// TODO: check process/appdomain permissions

			RefreshEventListenersList();
			List<ServiceEnvironmentEventListenerInfo> listeners;
			if (!_environmentListeners.TryGetValue(eventType, out listeners) || listeners.Count < 1)
				throw new ServiceEnvironmentException(String.Format("Could not find any registered listeners for {0} event.", eventType));

			foreach (var listenerInfo in listeners)
			{
				try
				{
					using (var client = new WcfClient<IServiceEnvironmentEventListener>(this, listenerInfo.EndpointName, listenerInfo.EndpointAddress))
					{
						listenerAction(client.Channel);
					}
				}
				catch (Exception ex)
				{
					string message = String.Format("Failed to send environment event {0} to listener at {1}, it might be dead.", eventType, listenerInfo.EndpointAddress);
					if (Service.Current != null)
						Service.Current.Log(message, ex, LogMessageType.Information);
					else
						throw new ServiceEnvironmentException(message, ex);
				}
			}
		}
		
		void IServiceEnvironmentEventSender.SendEnvironmentEvent(ServiceEnvironmentEventType eventType, Action<IServiceEnvironmentEventListener> listenerAction)
		{
			this.SendEnvironmentEvent(eventType, listenerAction);	
		}

 
	}

	internal class ServiceExecutionHostInfo
	{
		public string HostName;
		public Guid HostGuid;
		public string EndpointName;
		public string EndpointAddress;
	}

	internal class ServiceEnvironmentEventListenerInfo
	{
		public Guid ListenerID;
		public ServiceEnvironmentEventType EventType;
		public string EndpointName;
		public string EndpointAddress;
	}

	[Serializable]
	public class ServiceEnvironmentConfiguration : Lockable
	{
		public string ConnectionString;
		public string DefaultHostName;

		public string SP_HostListGet;
		public string SP_HostRegister;
		public string SP_HostUnregister;
		public string SP_InstanceSave;
		public string SP_InstanceReset;
        public string SP_InstanceGet;
        public string SP_InstanceActiveListGet;
		public string SP_EnvironmentEventListenerRegister;
		public string SP_EnvironmentEventListenerUnregister;
		public string SP_EnvironmentEventListenerListGet;
        public string SP_ServicesExecutionStatistics;
	}

	public interface IServiceEnvironmentEventSender
	{
		void SendEnvironmentEvent(ServiceEnvironmentEventType eventType, Action<IServiceEnvironmentEventListener> listenerAction);
	}
}
	