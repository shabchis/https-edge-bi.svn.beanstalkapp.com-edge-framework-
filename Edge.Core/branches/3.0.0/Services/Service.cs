using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Runtime.Remoting.Messaging;
using System.Threading;
using System.Runtime.Remoting.Contexts;
using System.ServiceModel;
using System.ServiceModel.Dispatcher;

namespace Edge.Core.Services
{
	public abstract class Service : MarshalByRefObject, IServiceInfo
	{
		#region Static
		//======================
		public static Service Current { get; private set; }
		
		public static readonly TimeSpan DefaultMaxExecutionTime = TimeSpan.FromMinutes(15);
		public static readonly TimeSpan MaxCleanupTime = TimeSpan.FromMinutes(1);
		
		//======================
		#endregion

		#region Instance
		//======================

		public Guid InstanceID { get; private set; }
		public ServiceConfiguration Configuration { get; private set; }
		public ServiceEnvironment Environment { get; private set; }
		public SchedulingInfo SchedulingInfo { get; private set; }
		public ServiceInstance ParentInstance { get; private set; }
		IServiceInfo IServiceInfo.ParentInstance { get { return this.ParentInstance; } }

		internal void Init(ServiceExecutionHost host, ServiceInstance instance)
		{
			_host = host;

			this.InstanceID = instance.InstanceID;
			this.Configuration = instance.Configuration;
			this.ParentInstance = instance.ParentInstance;
			this.Environment = instance.Environment;
			
			Current = this;
			
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
		int _resumeCount = 0;

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

		protected bool IsFirstRun
		{
			get { return _resumeCount == 0; }
		}


		//======================
		#endregion

		#region Communication
		//======================

		ServiceExecutionHost _host;

		void Notify(ServiceEventType eventType, object value)
		{
			lock (_connections)
			{
				foreach (IServiceConnection connection in this._connections.Values)
					connection.Notify(eventType, value);
			}
			
		}
		
		//[OneWay]
		internal void Connect(IServiceConnection connection)
		{
			lock (_connections)
			{
				_connections.Add(connection.Guid, connection);
			}
		}

		//[OneWay]
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

		object _controlSync = new object();
		internal bool IsStopped = false;
		Thread _doWork = null;

		//[OneWay]
		internal void Start()
		{
			if (this.State != ServiceState.Ready)
			{
				ReportError("Cannot start service that is not in the ready state.");
				return;
			}

			lock (_controlSync)
			{
				TimeStarted = DateTime.Now;
				DoWorkInternal();
			}
		}

		//[OneWay]
		internal void Resume()
		{
			if (this.State != ServiceState.Paused)
				return;

			_resumeCount++;
			DoWorkInternal();
		}

		void DoWorkInternal()
		{
			ServiceOutcome outcome = ServiceOutcome.Unspecified;

			lock (_controlSync)
			{
				State = ServiceState.Running;

				// Run the service code, and time its execution
				_doWork = new Thread(() =>
				{
					// Suppress thread abort because these are expected
					try { outcome = this.DoWork(); }
					catch (ThreadAbortException) { }
					catch (Exception ex)
					{
						ReportError("Error occured during execution.", ex);
						outcome = ServiceOutcome.Failure;
					}
				});
				_doWork.Start();

				if (!_doWork.Join(DefaultMaxExecutionTime))
				{
					// Timeout, abort the thread and exit
					_doWork.Abort();
					outcome = ServiceOutcome.Timeout;
				}

				_doWork = null;
			}

			if (outcome == ServiceOutcome.Unspecified)
				State = ServiceState.Paused;
			else
				Stop(outcome);
		}

		//[OneWay]
		protected internal void Abort()
		{
			lock (_controlSync)
			{
				if (State != ServiceState.Running && State != ServiceState.Ready && State != ServiceState.Paused)
					return;

				// Abort the worker thread
				if (_doWork != null)
					_doWork.Abort();
			}

			Stop(ServiceOutcome.Aborted);
		}

		void Stop(ServiceOutcome outcome)
		{
			lock (_controlSync)
			{
				// Enforce only one stop call
				if (IsStopped)
					return;

				IsStopped = true;

				// Report an outcome, bitch
				if (outcome == ServiceOutcome.Unspecified)
				{
					ReportError("Service did not report any outcome. Setting to failure.");
					outcome = ServiceOutcome.Failure;
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
						ReportError("Error occured during cleanup.", ex);
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
				Outcome = outcome;
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
				Stop(ServiceOutcome.Killed);
			}
		}

		void DomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			// Log the exception
			ReportError("Unhandled exception occured outside of DoWork.", e.ExceptionObject as Exception);
			Stop(ServiceOutcome.Failure);
		}

		protected ServiceInstance NewChildService(ServiceConfiguration child)
		{
			return Environment.NewServiceInstance(child, this);
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
