using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;
using System.Runtime.Remoting.Lifetime;
using System.Runtime.Remoting.Messaging;

namespace Edge.Core.Services2
{
	public class ServiceInstance: IServiceView
	{
		Service _serviceRef;
		ServiceSubscriber _subscriber;

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

		public event EventHandler StateChanged
		{
			add { this._serviceRef.Subscribe(_subscriber, ServiceEventType.StateChanged); }
			remove { 
		}
		public event EventHandler OutcomeReported;
		public event EventHandler ProgressReported;
		public event EventHandler OutputGenerated;

		public void Initialize()
		{
			Context.Host.InitializeService(this);
		}

		internal void AttachTo(Service service)
		{
			_serviceRef = service;
			ServiceSubscriber subscriber = new ServiceSubscriber();
		}

		private void UpdateState(ServiceEventType eventType, object value)
		{
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
	/// Objects that subcribes to service events and pushes them to the instance object.
	/// </summary>
	internal class ServiceSubscriber : MarshalByRefObject
	{
		internal ILease Lease { get; private set; }

		public override object InitializeLifetimeService()
		{
			this.Lease = (ILease)base.InitializeLifetimeService();
			return this.Lease;
		}

		public Action<ServiceEventType, object> EventReceived = null;

		[OneWay]
		internal void OnEvent(ServiceEventType eventType, object value)
		{
			if (EventReceived != null)
				EventReceived(eventType, value);
		}
	}
}
