using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Runtime.Remoting.Messaging;

namespace Edge.Core.Services2
{
	public abstract class Service : MarshalByRefObject, IServiceInfo
	{
		#region Static
		//======================

		public static readonly TimeSpan DefaultMaxExecutionTime = TimeSpan.FromMinutes(15);
		public static readonly TimeSpan MaxOnEndedTime = TimeSpan.FromMinutes(1);
		
		//======================
		#endregion

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

		public DateTime TimeStarted
		{
			get;
			private set;
		}

		public DateTime TimeEnded
		{
			get;
			private set;
		}

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

		object _sync = new object();
		bool _stopped = false;

		[OneWay]
		internal void Start()
		{
			ServiceOutcome outcome;
			
			lock (_sync)
			{
				if (this.State != ServiceState.Ready)
					throw new InvalidOperationException("Service can only be started when it is in the Ready state.");

				// Run the service code
				outcome = this.DoWork();
			}

			if (outcome != ServiceOutcome.Success && outcome != ServiceOutcome.Failure)
				throw new ServiceException("DoWork must return either Success or Failure.");

			// Stop and report outcome
			Stop(outcome);
		}

		[OneWay]
		protected internal void Abort()
		{
			if (State == ServiceState.Ending || State == ServiceState.Ended)
				return;

			// Stop the service
			Stop(ServiceOutcome.Aborted);
		}

		[OneWay]
		internal void Stop(ServiceOutcome outcome)
		{
			lock (_sync)
			{
				// Enforce only one stop call
				if (_stopped)
					return;

				_stopped = true;
				State = ServiceState.Ending;

				// Call finalizers (async with short timeout)
				var endedHandler = new Action<ServiceOutcome>(OnEnded);
				IAsyncResult ar = endedHandler.BeginInvoke(outcome, null, null);
				if (!ar.AsyncWaitHandle.WaitOne(MaxOnEndedTime))
					Log(String.Format("OnEnded timed out. Limit is {0}.", MaxOnEndedTime.ToString()), LogMessageType.Warning);
				try { endedHandler.EndInvoke(ar); }
				catch (Exception ex) { Log("Error occured in OnEnded.", ex); }

				State = ServiceState.Ended;
				
				_host.Unload(this.InstanceID);
			}
		}

		//======================
		#endregion

		#region For override
		//======================

		protected abstract ServiceOutcome DoWork();
		protected virtual void OnEnded(ServiceOutcome outcome) { }

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

	internal struct EventValue<T>
	{
		public DateTime Time;
		public T Value;
	}
}
