using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;
using System.Runtime.Remoting.Lifetime;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Serialization;

namespace Edge.Core.Services2
{
	public class ServiceInstance: IServiceView
	{
		double _progress = 0;
		Services.ServiceState _state;
		Services.ServiceOutcome _outcome;
		object _output = null;

		public Guid InstanceID { get; internal set; }
		public ServiceConfiguration Configuration { get; internal set; }
		public ServiceExecutionContext Context { get; internal set; }
		public ServiceInstance ParentInstance { get; internal set; }
		public SchedulingData SchedulingData { get; internal set; }
		public ReadOnlyObservableCollection<ServiceInstance> ChildInstances { get; private set; }

		public double Progress { get; private set; }
		public Edge.Core.Services.ServiceState State { get; private set; }
		public Edge.Core.Services.ServiceOutcome Outcome { get; private set; }
		public object Output { get; private set; }

		internal ServiceInstance()
		{
			this.Progress = 0;
			this.State = Services.ServiceState.Uninitialized;
			this.Outcome = Services.ServiceOutcome.Unspecified;
			this.Output = null;
		}

		#region Communication
		//======================

		Dictionary<ServiceEventType, EventHandler> _eventHandlers = new Dictionary<ServiceEventType, EventHandler>();

		public event EventHandler StateChanged
		{
			add { Subscribe(ServiceEventType.StateChanged, value); }
			remove { Unsubscribe(ServiceEventType.StateChanged, value); }
		}

		public event EventHandler OutcomeReported
		{
			add { Subscribe(ServiceEventType.OutcomeReported, value); }
			remove { Unsubscribe(ServiceEventType.OutcomeReported, value); }
		}

		public event EventHandler ProgressReported
		{
			add { Subscribe(ServiceEventType.ProgressReported, value); }
			remove { Unsubscribe(ServiceEventType.ProgressReported, value); }
		}

		public event EventHandler OutputGenerated
		{
			add { Subscribe(ServiceEventType.OutputGenerated, value); }
			remove { Unsubscribe(ServiceEventType.OutputGenerated, value); }
		}


		private void Subscribe(ServiceEventType serviceEventType, EventHandler value)
		{
			EventHandler handler;
			if (!_eventHandlers.TryGetValue(serviceEventType, out handler))
				this._serviceRef.Subscribe(serviceEventType, _subscriber);

			_eventHandlers[serviceEventType] = handler += value;
		}

		private void Unsubscribe(ServiceEventType serviceEventType, EventHandler value)
		{
			EventHandler handler;
			if (_eventHandlers.TryGetValue(serviceEventType, out handler))
			{
				handler -= value;
				if (handler == null)
				{
					this._serviceRef.Unsubscribe(serviceEventType, _subscriber);
					_eventHandlers.Remove(serviceEventType);
				}
				else
					_eventHandlers[serviceEventType] = handler;
			}
		}

		private void RaiseEvent(ServiceEventType eventType, EventArgs e)
		{
			EventHandler handler;
			if (_eventHandlers.TryGetValue(eventType, out handler))
				handler(this, e);
		}

		//======================
		#endregion

		public void Initialize()
		{
			this.Context.Host.InitializeService(this);
		}

		internal void AttachTo(Service service)
		{
			_serviceRef = service;
			_subscriber = new ServiceSubscriber() { EventReceived = ServiceEventReceived };

			foreach(KeyValuePair<ServiceEventType, EventHandler
		}

		private void ServiceEventReceived(ServiceEventType eventType, object value)
		{
			EventArgs e;
			switch (eventType)
			{
				case ServiceEventType.StateChanged:
					_state = (Services.ServiceState)value;
					e = EventArgs.Empty;
					break;
				case ServiceEventType.ProgressReported:
					_progress = (double)value;
					e = EventArgs.Empty;
					break;
				case ServiceEventType.OutputGenerated:
					_output = value;
					e = EventArgs.Empty;
					break;
				case ServiceEventType.OutcomeReported:
					_outcome = (Services.ServiceOutcome)value;
					e = EventArgs.Empty;
					break;
				default:
					return;
			}

			RaiseEvent(eventType, e);
		}

		public void Start()
		{
			var action = new Action(_serviceRef.Start);
			action.BeginInvoke(null, null);
		}

		public void Abort()
		{
			var action = new Action(_serviceRef.Abort);
			action.BeginInvoke(null, null);
		}

		public override string ToString()
		{
			return String.Format("{0} (profile: {1}, guid: {2})",
				Configuration.ServiceName,
				Configuration.Profile == null ? "default" : Configuration.Profile.Name,
				InstanceID
			);
		}
	}


	public class ServiceCreatedEventArgs : EventArgs
	{
		public ServiceInstance Instance
		{
			get;
			set;
		}
	}

	[Flags]
	public enum ServiceEventType
	{
		StateChanged = 0x001,
		OutcomeReported = 0x002,
		ProgressReported = 0x004,
		ChildCreated = 0x008,
		OutputGenerated = 0x010,
		All = 0xfff
	}

	/// <summary>
	/// Objects that listens for service events and pushes them to the instance object.
	/// </summary>
	internal interface IServiceListener
	{
		Action<ServiceEventType, object> EventReceived {get; set;}
		void ReceiveEvent(ServiceEventType eventType, object value);
	}

	/// <summary>
	/// Marshaled by ref, allows a local host to push events.
	/// </summary>
	internal class LocalServiceListener : MarshalByRefObject, IServiceListener
	{
		internal ILease Lease { get; private set; }

		public override object InitializeLifetimeService()
		{
			this.Lease = (ILease)base.InitializeLifetimeService();
			return this.Lease;
		}

		[OneWay]
		public void ReceiveEvent(ServiceEventType eventType, object value)
		{
			if (EventReceived != null)
				EventReceived(eventType, value);
		}


		public Action<ServiceEventType, object> EventReceived
		{
			get; 
			set;
		}
	}


	/// <summary>
	/// Serializes endpoint data for a remote host to know how to push back events back.
	/// </summary>
	[Serializable]
	internal class RemoteServiceListener : ISerializable, IServiceListener
	{
		#region ISerializable Members

		public void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			throw new NotImplementedException();
		}

		#endregion

		#region IServiceListener Members

		public Action<ServiceEventType, object> EventReceived
		{
			get
			{
				throw new NotImplementedException();
			}
			set
			{
				throw new NotImplementedException();
			}
		}

		public void ReceiveEvent(ServiceEventType eventType, object value)
		{
			throw new NotImplementedException();
		}

		#endregion
	}
}
