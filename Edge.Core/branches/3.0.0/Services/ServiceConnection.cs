using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Remoting.Lifetime;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Serialization;
using System.ServiceModel;

namespace Edge.Core.Services
{
	/// <summary>
	/// Objects that listens for service events and pushes them to the instance object.
	/// </summary>
	internal interface IServiceConnection: IDisposable
	{
		//IServiceExecutionHost Host { get; }
		Guid Guid { get; }
		Guid ServiceInstanceID { get; }

		[OperationContract(IsOneWay = true)]
		void ReceiveState(ServiceStateInfo stateInfo);
		
		[OperationContract(IsOneWay = true)]
		void ReceiveOutput(object output);
	}

	/*
	/// <summary>
	/// Marshaled by ref, allows a local host to push events.
	/// </summary>
	internal class LocalServiceConnection : MarshalByRefObject, IServiceConnection
	{
		public Action<ServiceEventType, object> EventCallback { get; set; }
		public IServiceHost Host { get; private set; }
		public Guid Guid { get; private set; }
		public Guid ServiceInstanceID { get; private set; }
		internal ILease Lease { get; private set; }

		public LocalServiceConnection(IServiceHost host, Guid serviceInstanceID)
		{
			this.Host = host;
			this.Guid = Guid.NewGuid();
			this.ServiceInstanceID = serviceInstanceID;
		}

		public override object InitializeLifetimeService()
		{
			this.Lease = (ILease)base.InitializeLifetimeService();
			return this.Lease;
		}

		[OneWay]
		public void Notify(ServiceEventType eventType, object value)
		{
			if (EventCallback != null)
				EventCallback(eventType, value);
		}

		public void Dispose()
		{
			this.Host.DisconnectService(this.ServiceInstanceID, this.Guid);
			// TODO: cause lease to expire
			//throw new NotImplementedException("The ILease must be released at this point to allow garbage collection!");
		}
	}
	*/

	/// <summary>
	/// Serializes endpoint data for a remote host to know how to push back events back.
	/// </summary>
	[CallbackBehavior(UseSynchronizationContext = false, ConcurrencyMode = ConcurrencyMode.Single)]
	internal class ServiceConnection : IServiceConnection
	{
		public Action<ServiceStateInfo> StateChangedCallback { get; set; }
		public Action<object> OutputGeneratedCallback { get; set; }
		public WcfDuplexClient<IServiceExecutionHost> Host { get; private set; }
		public Guid Guid { get; private set; }
		public Guid ServiceInstanceID { get; private set; }

		internal ServiceConnection(ServiceEnvironment environment, Guid serviceInstanceID, string endpointName, string endpointAddress)
		{
			this.Guid = Guid.NewGuid();
			this.ServiceInstanceID = serviceInstanceID;
			this.Host = new WcfDuplexClient<IServiceExecutionHost>(environment, this, endpointName, endpointAddress);
			//TODO: add environment to StreamingContext
			this.Host.Open();
			this.Host.Channel.Connect(this.ServiceInstanceID, this.Guid);
		}

		internal void RefreshState()
		{
			this.Host.Channel.NotifyState(this.ServiceInstanceID);
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
			if (Host != null)
			{
				if (Host.State == CommunicationState.Opened)
				{
					Host.Channel.Disconnect(this.ServiceInstanceID, this.Guid);
					Host.Close();
				}
				else if (Host.State != CommunicationState.Closed)
					Host.Abort();
			}
		}
	}
}
