using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Runtime.Remoting.Messaging;
using System.Threading;

namespace Edge.Core.Services2
{
	public abstract class Service : MarshalByRefObject, IServiceInfo
	{
		#region Static
		//======================

		public static readonly TimeSpan DefaultMaxExecutionTime = TimeSpan.FromMinutes(15);
		public static readonly TimeSpan MaxCleanupTime = TimeSpan.FromMinutes(1);
		
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

			TimeInitialized = DateTime.Now;
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

		public DateTime TimeInitialized
		{
			get;
			private set;
		}

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
			private set
			{
				if (_outcome != ServiceOutcome.Unspecified || value == ServiceOutcome.Unspecified)
					return;

				Notify(ServiceEventType.OutcomeReported, _outcome = value);
			}
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
		internal bool IsStopped = false;
		bool _exceptionThrown = false;

		[OneWay]
		internal void Start()
		{
			lock (_sync)
			{
				if (this.State != ServiceState.Ready)
					throw new InvalidOperationException("Service can only be started when it is in the Ready state.");

				TimeStarted = DateTime.Now;
				State = ServiceState.InProgress;

				// Run the service code, and time its execution
				Thread doWorkThread = new Thread(() =>
				{
					// Suppress thread abort because these are expected
					try { Outcome = this.DoWork(); }
					catch (ThreadAbortException) { }
				});
				doWorkThread.Start();

				if (!doWorkThread.Join(DefaultMaxExecutionTime))
				{
					// Timeout, abort the thread and exit
					doWorkThread.Abort();
					Outcome = ServiceOutcome.Timeout;
				}
			}

			// Exit
			Stop();
		}

		[OneWay]
		protected internal void Abort()
		{
			if (State == ServiceState.Ending || State == ServiceState.Ended)
				return;

			// Stop the service
			Outcome = ServiceOutcome.Aborted;
			Stop();
		}

		void Stop()
		{
			lock (_sync)
			{
				// Enforce only one stop call
				if (IsStopped)
					return;

				IsStopped = true;

				// Report an outcome, bitch
				if (this.Outcome == ServiceOutcome.Unspecified)
				{
					this.Output = new ServiceException("Service did not report any outcome.");
					this.Outcome = ServiceOutcome.Error;
				}

				// Start wrapping things up
				State = ServiceState.Ending;

				// 
				// Run the service code, and time its execution
				Thread onEndedThread = new Thread(() =>
				{
					// Suppress thread abort because these are expected
					try { this.Cleanup(); }
					catch (ThreadAbortException) { }
					catch (Exception ex)
					{
						Log("Error occured during cleanup.", ex);
					}
				});
				onEndedThread.Start();

				if (!onEndedThread.Join(MaxCleanupTime))
				{
					// Timeout, abort the thread and exit
					onEndedThread.Abort();
					Log(String.Format("Cleanup timed out. Limit is {0}.", MaxCleanupTime.ToString()), LogMessageType.Error);
				}

				// Change state to ended
				State = ServiceState.Ended;
			}

			// Unload app domain if Stop was called directly
			AppDomain.Unload(AppDomain.CurrentDomain);
		}

		void DomainUnload(object sender, EventArgs e)
		{
			// If we need to stop from here it means an external appdomain called an unload
			if (!IsStopped)
			{
				if (!_exceptionThrown)
					Log("Service's AppDomain is being unloaded.", LogMessageType.Warning);

				Stop();
			}
		}

		void DomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			// Mark the outcome as Error
			_exceptionThrown = true;

			// Log the exception
			Log("Unhandled exception occured.", e.ExceptionObject as Exception);

			// Output the exception
			Output = e.ExceptionObject;
			Outcome = ServiceOutcome.Error;
		}

		//======================
		#endregion

		#region For override
		//======================

		protected abstract ServiceOutcome DoWork();

		/// <summary>
		/// When overridden in a derived class, can perform last minute finalization before a service ends (even if it fails). When cleanup is called,
		/// this.Outcome has already been set and can be used to rollback failed operations if necessary.
		/// </summary>
		protected virtual void Cleanup() { }

		//======================
		#endregion

		#region Logging
		//======================

		protected void Log(LogMessage message)
		{
			//throw new NotImplementedException();
		}

		protected void Log(string message, Exception ex)
		{
			//throw new NotImplementedException();
		}

		protected void Log(string message, LogMessageType messageType)
		{
			//throw new NotImplementedException();
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
