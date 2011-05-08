using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Lifetime;
using System.Text;

namespace Edge.Core.Services2
{
	public class ServiceProxy: ServiceInstance
	{
		public event EventHandler StateChanged;
		public event EventHandler OutcomeReported;
		public event EventHandler ProgressReported;
		public event EventHandler OutputGenerated;

		public void Start()
		{
			var action = new Action(this.ServiceRef.Start);
			action.BeginInvoke(null, null);
		}

		public void Abort()
		{
			var action = new Action(this.ServiceRef.Abort);
			action.BeginInvoke(null, null);
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

		public Action<ServiceEventType, object> NotifyCallback = null;

		public void Notify(ServiceEventType eventType, object value)
		{
			if (NotifyCallback != null)
				NotifyCallback(eventType, value);
		}
	}
}
