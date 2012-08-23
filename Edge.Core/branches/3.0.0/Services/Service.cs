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

		public Guid InstanceID { get; private set; }
		public ServiceConfiguration Configuration { get; private set; }
		public ServiceEnvironment Environment { get; private set; }
		public ServiceExecutionInfo ExecutionInfo { get; private set; }
		public ServiceInstance ParentInstance { get; private set; }

		IServiceExecutionHost _host;
		int _resumeCount = 0;
		internal bool IsStopped = false;
		Thread _doWork = null;

		internal void Init(IServiceExecutionHost host, ServiceInstance instance)
		{
			_host = host;

			this.InstanceID = instance.InstanceID;
			this.Configuration = instance.Configuration;
			this.ParentInstance = instance.ParentInstance;
			this.Environment = new ServiceEnvironment(_host);
			this.ExecutionInfo = new ServiceExecutionInfo();
			
			Current = this;
			
			// Monitor app domain-level events
			AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(this.DomainUnhandledException);
			AppDomain.CurrentDomain.DomainUnload += new EventHandler(this.DomainUnload);

			this.ExecutionInfo.TimeInitialized = DateTime.Now;
			this.ExecutionInfo.State = ServiceState.Ready;
			Updated(ServiceUpdateType.State);
		}

		
		protected bool IsFirstRun
		{
			get { return _resumeCount == 0; }
		}

		public ServiceState State
		{
			get { return this.ExecutionInfo.State; }
		}

		void Updated(ServiceUpdateType eventType)
		{
			_host.ServiceUpdate(this.InstanceID, eventType);
		}

		protected void SendOutput(object output)
		{
			_host.ServiceOutput(this.InstanceID, output);
		}

		protected void Error(Exception exception, bool fatal = false)
		{
			_host.ServiceOutput(this.InstanceID, exception);
			if (fatal)
				Stop(ServiceOutcome.Failure);
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
				Error("Cannot start service that is not in the ready state.");

			ExecutionInfo.TimeStarted = DateTime.Now;
			DoWorkInternal();
		}

		internal void Resume()
		{
			if (this.State != ServiceState.Paused)
				Error("Cannot resume service that is not in the paused state.");

			_resumeCount++;
			DoWorkInternal();
		}

		void DoWorkInternal()
		{
			ServiceOutcome outcome = ServiceOutcome.Unspecified;

			ExecutionInfo.State = ServiceState.Running;
			Updated(ServiceUpdateType.State);

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
				ExecutionInfo.State = ServiceState.Paused;
				Updated(ServiceUpdateType.State);
			}
			else
				Stop(outcome);
		}

		//[OneWay]
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

			// Start wrapping things up
			ExecutionInfo.State = ServiceState.Ending;
			Updated(ServiceUpdateType.State);

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
			ExecutionInfo.State = ServiceState.Ended;
			ExecutionInfo.Outcome = outcome;
			Updated(ServiceUpdateType.State);

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

		/*
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
		*/
		//======================
		#endregion
	}

	/*
	[Serializable]
	internal struct EventValue<T>
	{
		public DateTime Time;
		public T Value;
	}
	*/
}
