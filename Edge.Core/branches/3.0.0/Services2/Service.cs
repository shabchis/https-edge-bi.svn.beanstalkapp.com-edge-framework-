using System;
using System.Collections.Generic;
using System.IO;
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

			// Monitor app domain-level events
			AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(this.DomainUnhandledException);
			AppDomain.CurrentDomain.DomainUnload += new EventHandler(this.DomainUnload);

			// TODO: Reroute console output to verbose Log
			//Console.SetOut(

			TimeInitialized = DateTime.Now;
			State = ServiceState.Ready;
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
			private set { Notify(ServiceEventType.StateChanged, new EventValue<ServiceState>() {Time = DateTime.Now, Value = _state = value }); }
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
		readonly Dictionary<Guid, IServiceConnection> _connections = new Dictionary<Guid, IServiceConnection>();

		void Notify(ServiceEventType eventType, object value)
		{
			lock (_connections)
			{
				foreach (IServiceConnection connection in this._connections.Values)
					connection.Notify(eventType, value);
			}
		}
		
		[OneWay]
		internal void Connect(IServiceConnection connection)
		{
			lock (_connections)
			{
				_connections.Add(connection.Guid, connection);
			}
		}

		[OneWay]
		internal void Disconnect(Guid connectionGuid)
		{
			lock (_connections)
			{
				_connections.Remove(connectionGuid);
			}
		}

		//======================
		#endregion

		#region Control
		//======================

		object _sync = new object();
		internal bool IsStopped = false;
		Thread _doWork = null;

		[OneWay]
		internal void Start()
		{
			lock (_sync)
			{
				if (this.State != ServiceState.Ready)
					return;

				TimeStarted = DateTime.Now;
				State = ServiceState.Running;

				// Run the service code, and time its execution
				_doWork = new Thread(() =>
				{
					// Suppress thread abort because these are expected
					try { Outcome = this.DoWork(); }
					catch (ThreadAbortException) { }
					catch (Exception ex)
					{
						ReportError("Error occured during execution.", ex);
					}
				});
				_doWork.Start();

				if (!_doWork.Join(DefaultMaxExecutionTime))
				{
					if (Outcome == ServiceOutcome.Unspecified)
					{
						// Timeout, abort the thread and exit
						_doWork.Abort();
						Outcome = ServiceOutcome.Timeout;
					}
				}

				_doWork = null;
			}

			// Exit
			Stop();
		}

		[OneWay]
		protected internal void Abort()
		{
			lock (_sync)
			{
				if (State != ServiceState.Running && State != ServiceState.Ready && State != ServiceState.Waiting)
					return;

				// Abort the worker thread
				if (_doWork != null)
					_doWork.Abort();

				// notify
				Outcome = ServiceOutcome.Aborted;
			}

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
					ReportError("Service did not report any outcome.");

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
				Log("Service's AppDomain is being unloaded by external code.", LogMessageType.Warning);
				Stop();
			}
		}

		void DomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			// Log the exception
			ReportError("Unhandled exception occured outside of DoWork.", e.ExceptionObject as Exception);
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

		#region Logging and error handling
		//======================

		protected void Log(LogMessage message)
		{
			if (message.Source != null)
				throw new InvalidOperationException("The LogMessage.Source property must be null.");

			message.Source = this.Configuration.ServiceName;

			_host.Log(this.InstanceID, message);
		}

		protected void Log(string message, Exception ex, LogMessageType messageType = LogMessageType.Error)
		{
			this.Log(new LogMessage()
			{
				Message = message,
				MessageType = messageType,
				Exception = ex
			});
		}

		protected void Log(string message, LogMessageType messageType)
		{
			this.Log(new LogMessage()
			{
				Message = message,
				MessageType = messageType
			});
		}

		protected void ReportError(string message, Exception ex = null)
		{
			LogMessage lm = new LogMessage()
			{
				Message = message,
				MessageType = LogMessageType.Error,
				Exception = ex
			};

			Log(lm);

			// Output the exception
			Output = lm;
			Outcome = ServiceOutcome.Error;
		}

		//======================
		#endregion
	}

	[Serializable]
	internal struct EventValue<T>
	{
		public DateTime Time;
		public T Value;
	}
}
