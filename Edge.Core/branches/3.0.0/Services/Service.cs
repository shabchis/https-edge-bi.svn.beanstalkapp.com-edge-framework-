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
using Edge.Core.Utilities;

namespace Edge.Core.Services
{
	public abstract class Service : MarshalByRefObject
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
		internal ServiceStateInfo StateInfo;
		internal ServiceExecutionHost Host;
		internal bool IsStopped = false;
		Thread _doWork = null;

		public Guid InstanceID { get; private set; }
		public ServiceConfiguration Configuration { get; private set; }
		public SchedulingInfo SchedulingInfo { get; private set; }
		public ServiceEnvironment Environment { get; private set; }
		public ServiceInstance ParentInstance { get; private set; }
		public double Progress { get { return StateInfo.Progress; } protected set { StateInfo.Progress = value; NotifyState(); } }
		public ServiceState State { get { return StateInfo.State; } }
		public ServiceOutcome Outcome { get { return StateInfo.Outcome; } }
		public DateTime TimeInitialized { get { return StateInfo.TimeInitialized; } }
		public DateTime TimeStarted { get { return StateInfo.TimeStarted; } }
		public DateTime TimeEnded { get { return StateInfo.TimeEnded; } }
		public DateTime TimeLastPaused { get { return StateInfo.TimeLastPaused; } }
		public DateTime TimeLastResumed { get { return StateInfo.TimeLastResumed; } }
		

		internal void Init(ServiceExecutionHost host, ServiceEnvironmentConfiguration envConfig, ServiceConfiguration config, SchedulingInfo schedulingInfo, Guid instanceID, Guid parentInstanceID)
		{
			Host = host;
			this.Environment = ServiceEnvironment.Open(envConfig);

			this.InstanceID = instanceID;
			this.Configuration = config;
			this.SchedulingInfo = schedulingInfo;

			if (parentInstanceID != Guid.Empty)
				this.ParentInstance = Environment.GetServiceInstance(parentInstanceID);
			
			Current = this;
			
			// Monitor app domain-level events
			AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(this.DomainUnhandledException);
			AppDomain.CurrentDomain.DomainUnload += new EventHandler(this.DomainUnload);

			StateInfo.TimeInitialized = DateTime.Now;
			StateInfo.State = ServiceState.Ready;
			NotifyState();
		}

		
		protected bool IsFirstRun
		{
			get { return StateInfo.ResumeCount == 0; }
		}

		void NotifyState()
		{
			try { Host.UpdateState(this.InstanceID, StateInfo); }
			catch (Exception ex)
			{
				try { Log("Could not notify environment of service state, possibly because the host is being disposed.", ex, LogMessageType.Warning); }
				catch { }
			}
		}

		protected void GenerateOutput(object output)
		{
			try { Host.NotifyOutput(this.InstanceID, output); }
			catch (Exception ex)
			{
				try { Log("Could not send output from service, possibly because the host is being disposed.", ex, LogMessageType.Warning); }
				catch { }
			}

		}

		protected void Error(Exception exception, bool fatal = false)
		{
			if (fatal)
				throw exception;
			else
			{
				GenerateOutput(exception);
			}
		}

		protected void Error(string message, Exception inner = null, bool fatal = false)
		{
			this.Error(new ServiceException(message, inner), fatal);
		}

		protected virtual string GetLogContextInfo()
		{
			return null;
		}
		
		//======================
		#endregion

		#region Control
		//======================

		[OneWay]
		internal void Start()
		{
			if (State != ServiceState.Ready)
			{
				Error("Cannot start service that is not in the ready state.");
				return;
			}

			StateInfo.TimeStarted = DateTime.Now;
			DoWorkInternal();
		}

		internal void Resume()
		{
			if (this.State != ServiceState.Paused)
			{
				Error("Cannot resume service that is not in the paused state.");
				return;
			}

			StateInfo.ResumeCount++;
			StateInfo.TimeLastResumed = DateTime.Now;
			DoWorkInternal();
		}

		void DoWorkInternal()
		{
			ServiceOutcome outcome = ServiceOutcome.Unspecified;

			StateInfo.State = ServiceState.Running;
			NotifyState();

			// Run the service code, and time its execution
			_doWork = new Thread(() =>
			{
				// Suppress thread abort because these are expected
				try { outcome = this.DoWork(); }
				catch (ThreadAbortException) { }
				catch (Exception ex)
				{
					Error("Error occured during execution.", ex);
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

			if (outcome == ServiceOutcome.Unspecified)
			{
				StateInfo.State = ServiceState.Paused;
				StateInfo.TimeLastPaused = DateTime.Now;
				NotifyState();
			}
			else
				Stop(outcome);
		}

		[OneWay]
		protected internal void Abort()
		{
			if (State != ServiceState.Running && State != ServiceState.Ready && State != ServiceState.Paused)
			{
				Error("Service can only be aborted in running, ready or paused state.");
				return;
			}

			// Abort the worker thread
			if (_doWork != null)
				_doWork.Abort();

			Stop(ServiceOutcome.Aborted);
		}

		void Stop(ServiceOutcome outcome)
		{
			// Enforce only one stop call
			if (IsStopped)
				return;

			IsStopped = true;

			// Report an outcome, bitch
			if (outcome == ServiceOutcome.Unspecified)
			{
				Error("Service did not report any outcome, treating as failure.");
				outcome = ServiceOutcome.Failure;
			}
			else if (outcome == ServiceOutcome.Success)
			{
				StateInfo.Progress = 1;
			}

			// Start wrapping things up
			StateInfo.State = ServiceState.Ending;
			NotifyState();

			// Run the cleanup code, and time its execution
			Thread onEndedThread = new Thread(() =>
			{
				// Suppress thread abort because these are expected
				try { this.Cleanup(); }
				catch (ThreadAbortException) { }
				catch (Exception ex)
				{
					Error("Error occured during cleanup.", ex);
				}
			});
			onEndedThread.Start();

			if (!onEndedThread.Join(MaxCleanupTime))
			{
				// Timeout, abort the thread and exit
				onEndedThread.Abort();
				//Log(String.Format("Cleanup timed out. Limit is {0}.", MaxCleanupTime.ToString()), LogMessageType.Error);
			}

			// Change state to ended
			StateInfo.TimeEnded = DateTime.Now;
			StateInfo.State = ServiceState.Ended;
			StateInfo.Outcome = outcome;
			NotifyState();

			// Unload app domain if Stop was called directly
			AppDomain.Unload(AppDomain.CurrentDomain);
		}

		[System.Runtime.Remoting.Messaging.OneWay]
		internal void Kill()
		{
			// Unload the AppDomain, start the stop process
			AppDomain.Unload(AppDomain.CurrentDomain);
		}

		void DomainUnload(object sender, EventArgs e)
		{
			// If we need to stop from here it means an external appdomain called an unload
			if (!IsStopped)
			{
				Error("Service's AppDomain is being unloaded by external code.");
				Stop(ServiceOutcome.Killed);
			}
		}

		void DomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			// Log the exception
			Error("Unhandled exception occured outside of DoWork.", e.ExceptionObject as Exception);
			Stop(ServiceOutcome.Failure);
		}

		protected ServiceInstance NewChildService(ServiceConfiguration child)
		{
			return Environment.NewServiceInstance(child, ServiceInstance.FromService(this));
		}

		public ServiceInstance AsServiceInstance()
		{
			return ServiceInstance.FromService(this);
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

		public void Log(string message, Exception ex, LogMessageType messageType = LogMessageType.Error)
		{
			this.Host.InstanceLog(
				instanceID: this.InstanceID,
				profileID: this.Configuration.Profile != null ? this.Configuration.Profile.ProfileID : Guid.Empty,
				serviceName: this.Configuration.ServiceName,
				contextInfo: this.GetLogContextInfo(),
				message: message,
				ex: ex,
				messageType: messageType
			);
		}

		public void Log(string message, LogMessageType messageType)
		{
			this.Log(message, null, messageType);
		}

		
		//======================
		#endregion
	}
}
