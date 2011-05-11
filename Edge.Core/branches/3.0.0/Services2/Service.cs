using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Runtime.Remoting.Messaging;

namespace Edge.Core.Services2
{
	public abstract class Service : MarshalByRefObject, IServiceView
	{
		#region Instance
		//======================

		public Guid InstanceID { get; private set; }
		public ServiceConfiguration Configuration { get; private set; }
		public ServiceEnvironment Environment { get; private set; }
		public ServiceExecutionContext Context { get; private set; }
		public ServiceInstance ParentInstance { get; private set; }
		public SchedulingInfo SchedulingInfo { get; private set; }
		public System.Collections.ObjectModel.ReadOnlyObservableCollection<ServiceInstance> ChildInstances
		{
			get { throw new NotImplementedException(); }
		}

		internal void Init(ServiceExecutionHost host, ServiceInstance instance)
		{
			_host = host;

			this.InstanceID = instance.InstanceID;
			this.Configuration = instance.Configuration;
			this.Context = instance.Context;
			this.ParentInstance = instance.ParentInstance;
			this.SchedulingInfo = instance.SchedulingInfo;

			Notify(ServiceEventType.StateChanged, ServiceState.Ready);
		}

		//======================
		#endregion

		#region State
		//======================

		double _progress = 0;
		ServiceState _state = ServiceState.Initializing;
		ServiceOutcome _outcome = ServiceOutcome.Unspecified;
		object _output = null;

		public double Progress
		{
			get { return _progress; }
			protected set { Notify(ServiceEventType.ProgressReported, _progress = value); }
		}

		public ServiceState State
		{
			get { return _state; }
			private set { Notify(ServiceEventType.StateChanged, _state = value); }
		}

		public ServiceOutcome Outcome
		{
			get { return _outcome; }
			private set { Notify(ServiceEventType.OutcomeReported, _outcome = value); }
		}

		public object Output
		{
			get { return _output; }
			private set { Notify(ServiceEventType.OutputGenerated, _output = value); }
		}


		//======================
		#endregion

		#region Communication
		//======================

		ServiceExecutionHost _host;
		internal readonly List<IServiceConnection> Connections = new List<IServiceConnection>();

		void Notify(ServiceEventType eventType, object value)
		{
			lock (Connections)
			{
				foreach (IServiceConnection connection in this.Connections)
					connection.Notify(eventType, value);
			}
		}

		//======================
		#endregion

		#region Control
		//======================

		[OneWay]
		internal void Start()
		{
		}

		[OneWay]
		internal protected void Abort()
		{
		}

		//======================
		#endregion

		#region For override
		//======================

		protected abstract ServiceOutcome DoWork();
		protected virtual void OnEnded(ServiceState state) { }

		//======================
		#endregion

		#region Logging
		//======================

		protected void Log(LogMessage message)
		{
			throw new NotImplementedException();
		}

		protected void Log(string message, Exception ex)
		{
			throw new NotImplementedException();
		}

		protected void Log(string message, LogMessageType messageType)
		{
			throw new NotImplementedException();
		}

		//======================
		#endregion
	}
}
