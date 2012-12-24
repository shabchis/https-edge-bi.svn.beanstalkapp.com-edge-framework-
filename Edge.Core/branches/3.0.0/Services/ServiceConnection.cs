using System;
using System.ServiceModel;

namespace Edge.Core.Services
{
	/// <summary>
	/// Serializes endpoint data for a remote host to know how to push back events back.
	/// </summary>
	[CallbackBehavior(UseSynchronizationContext = false, ConcurrencyMode = ConcurrencyMode.Single)]
	internal class ServiceConnection : IServiceConnection
	{
		public Action<ServiceStateInfo> StateChangedCallback { get; set; }
		public Action<object> OutputGeneratedCallback { get; set; }
		public WcfDuplexClient<IServiceExecutionHost> HostChannel { get; private set; }
		public Guid Guid { get; private set; }
		public Guid ServiceInstanceID { get; private set; }
		public string EnvironmentUsageName { get; private set; }

		internal ServiceConnection(ServiceEnvironment environment, Guid serviceInstanceID, string endpointName, string endpointAddress)
		{
			Guid = Guid.NewGuid();
			ServiceInstanceID = serviceInstanceID;
			EnvironmentUsageName = environment.UsageName;
			HostChannel = new WcfDuplexClient<IServiceExecutionHost>(environment, this, endpointName, endpointAddress);
			//TODO: add environment to StreamingContext
			HostChannel.Open();
			HostChannel.Channel.Connect(ServiceInstanceID, Guid, EnvironmentUsageName);
		}

		internal void RefreshState()
		{
			HostChannel.Channel.NotifyState(ServiceInstanceID);
		}

		void IServiceConnection.ReceiveState(ServiceStateInfo stateInfo)
		{
			if (StateChangedCallback != null)
				StateChangedCallback(stateInfo);
		}

		void IServiceConnection.ReceiveOutput(object output)
		{
			if (OutputGeneratedCallback != null)
				OutputGeneratedCallback(output);
		}

		public void Dispose()
		{
			// Close the channel if it is still open
			if (HostChannel != null)
			{
				if (HostChannel.State == CommunicationState.Opened)
				{
					HostChannel.Channel.Disconnect(ServiceInstanceID, Guid);
					HostChannel.Close();
				}
				else if (HostChannel.State != CommunicationState.Closed)
					HostChannel.Abort();
			}
		}
	}
}
