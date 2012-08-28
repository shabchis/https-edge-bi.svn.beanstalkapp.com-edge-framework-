﻿using System;
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
		ServiceStateInfo _stateInfo;
		internal ServiceExecutionHost Host;
		int _resumeCount = 0;
		internal bool IsStopped = false;
		Thread _doWork = null;

		public Guid InstanceID { get; private set; }
		public ServiceConfiguration Configuration { get; private set; }
		public ServiceEnvironment Environment { get; private set; }
		public ServiceInstance ParentInstance { get; private set; }
		public double Progress { get { return _stateInfo.Progress; } protected set { _stateInfo.Progress = value; NotifyState(); } }
		public ServiceState State { get { return _stateInfo.State; } }
		public ServiceOutcome Outcome { get { return _stateInfo.Outcome; } }
		public DateTime TimeInitialized { get { return _stateInfo.TimeInitialized; } }
		public DateTime TimeStarted { get { return _stateInfo.TimeStarted; } }
		public DateTime TimeEnded { get { return _stateInfo.TimeEnded; } }
		

		internal void Init(ServiceExecutionHost host, ServiceEnvironmentConfiguration envConfig, ServiceConfiguration config, Guid instanceID, Guid parentInstanceID)
		{
			Host = host;
			this.Environment = new ServiceEnvironment(envConfig);

			this.InstanceID = instanceID;
			this.Configuration = config;
			//((ILockable)this.Configuration).Lock();
			if (parentInstanceID != Guid.Empty)
				this.ParentInstance = Environment.GetServiceInstance(parentInstanceID);
			
			Current = this;
			
			// Monitor app domain-level events
			AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(this.DomainUnhandledException);
			AppDomain.CurrentDomain.DomainUnload += new EventHandler(this.DomainUnload);

			_stateInfo.TimeInitialized = DateTime.Now;
			_stateInfo.State = ServiceState.Ready;
			NotifyState();
		}

		
		protected bool IsFirstRun
		{
			get { return _resumeCount == 0; }
		}

		void NotifyState()
		{
			Host.NotifyState(this.InstanceID, _stateInfo);
		}

		protected void GenerateOutput(object output)
		{
			Host.NotifyOutput(this.InstanceID, output);
		}

		protected void Error(Exception exception, bool fatal = false)
		{
			if (fatal)
				throw exception;
			else
				Host.NotifyOutput(this.InstanceID, exception);
		}

		protected void Error(string message, Exception inner = null, bool fatal = false)
		{
			this.Error(new ServiceException(message, inner), fatal);
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

			_stateInfo.TimeStarted = DateTime.Now;
			DoWorkInternal();
		}

		internal void Resume()
		{
			if (this.State != ServiceState.Paused)
			{
				Error("Cannot resume service that is not in the paused state.");
				return;
			}

			_resumeCount++;
			DoWorkInternal();
		}

		void DoWorkInternal()
		{
			ServiceOutcome outcome = ServiceOutcome.Unspecified;

			_stateInfo.State = ServiceState.Running;
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
				_stateInfo.State = ServiceState.Paused;
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
				_stateInfo.Progress = 1;
			}

			// Start wrapping things up
			_stateInfo.State = ServiceState.Ending;
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
			_stateInfo.State = ServiceState.Ended;
			_stateInfo.Outcome = outcome;
			NotifyState();

			// Unload app domain if Stop was called directly
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
			return Environment.NewServiceInstance(child, ServiceInstance.ForService(this));
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
			if (message.Source != null)
				throw new InvalidOperationException("The LogMessage.Source property must be null.");

			message.Source = this.Configuration.ServiceName;

			this.Host.Log(this.InstanceID, message);
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
		
		//======================
		#endregion
	}
}
