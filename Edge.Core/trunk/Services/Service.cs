using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Reflection;
using System.ServiceModel;
using System.Threading;
using Edge.Core.Configuration;
using Edge.Core.Data;
using Edge.Core.Utilities;
using System.Runtime.Remoting;
using System.ServiceModel.Channels;
using System.ServiceModel.Dispatcher;
using System.Collections.ObjectModel;
using System.ServiceModel.Description;
using System.ComponentModel;

namespace Edge.Core.Services
{
	/// <summary>
	/// Core class of the service-oriented architecture.
	/// </summary>
	[ServiceBehavior(InstanceContextMode=InstanceContextMode.Single, ConcurrencyMode=ConcurrencyMode.Single)]
	public class Service: IServiceEngine, IDisposable, IErrorHandler
	{
		#region Fields
		/*=========================*/
		static Service _current = null;

		Dictionary<ServiceEventType, List<IServiceSubscriber>> _subscribers =
			new Dictionary<ServiceEventType,List<IServiceSubscriber>>();

		ServiceInstanceInfo _instance;
		ServiceWorkflowContext _workflowContext;
		ServiceState _state = ServiceState.Ready;
		ServiceOutcome _outcome = ServiceOutcome.Unspecified;

		List<StepInfo> _stepHistory = new List<StepInfo>();
		
		Process _process;
		EventHandler _processEndHandler;
		
		ServiceHost _wcfHost;

		List<EvaluatorVariable> _globalConditionVars;

		System.Timers.Timer _executionTimer;

		object _stopLock = new object();

		bool _stopped = false;
		bool _exceptionThrown = false;

		/*=========================*/
		#endregion

		#region Constructors
		/*=========================*/
		protected internal Service()
		{
			if (_current != null)
				throw new InvalidOperationException("The current AppDomain already contains a Service object. Only one Service object can be created per AppDomain.");

			_current = this;

			AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(UnhandledException);
			AppDomain.CurrentDomain.DomainUnload += new EventHandler(DomainUnload);
		}

		/*=========================*/
		#endregion

		#region Public Properties
		/*=========================*/

		public static Service Current
		{
			get { return _current; }
		}

		//internal Log Log
		//{
		//    get { return _log; }
		//}

		public ServiceInstanceInfo Instance
		{
			get { return _instance; }
		}

		public ServiceWorkflowContext WorkflowContext
		{
			get { return _workflowContext; }
		}

		/// <summary>
		/// 
		/// </summary>
		public ServiceState State
		{
			get
			{
				return _state;
			}

			private set
			{
				ServiceState before = State;
				
				// Throw exception if invalid operations are being performed
				if (value == ServiceState.Uninitialized)
					throw new InvalidOperationException("State cannot be manually set to Uninitialized.");
				
				if (before != ServiceState.Uninitialized && value == ServiceState.Ready)
					throw new InvalidOperationException("State cannot be manually set to Ready after the service has started.");
				
				if (before == ServiceState.Ended)
					throw new InvalidOperationException("State can only be set to Ended once per service lifetime.");

				// Apply the value
				_state = value;
				
				if (before != value && _subscribers.ContainsKey(ServiceEventType.StateChanged))
				{
					_subscribers[ServiceEventType.StateChanged].ForEach(delegate(IServiceSubscriber subscriber)
					{
						try
						{
							subscriber.StateChanged(value);
						}
						catch(Exception ex)
						{
							Log.Write("Failed to notify the instance proxy (ServiceInstance object) of an engine event.", ex);
						}
					});
				}
			}
		}

		/// <summary>
		/// 
		/// </summary>
		public ServiceOutcome Outcome
		{
			get
			{
				return _outcome;
			}
		}

		/*=========================*/
		#endregion

		#region CreateInstance
		/*=========================*/

		/// <summary>
		/// 
		/// </summary>
		public static ServiceInstance CreateInstance(EnabledConfigurationElement configuration)
		{
			return ServiceInstance.Generate(configuration, null, -1);
		}

		/// <summary>
		/// 
		/// </summary>
		public static ServiceInstance CreateInstance(EnabledConfigurationElement configuration, int accountID)
		{
			return ServiceInstance.Generate(configuration, null, accountID);
		}

		/// <summary>
		/// 
		/// </summary>
		public static ServiceInstance CreateInstance(EnabledConfigurationElement configuration, ServiceInstance parentInstance, int accountID)
		{
			return ServiceInstance.Generate(configuration, parentInstance, accountID);
		}

		/*=========================*/
		#endregion

		#region Initialization
		/*=========================*/

		/// <summary>
		/// 
		/// </summary>
		internal void Init(ServiceInstanceInfo instance)
		{
			_instance = instance;

			if (_wcfHost == null)
			{
				_wcfHost = new ServiceEngineHost(this);

				Binding binding = ServiceEngineCommChannel.GetBinding(instance);

				// Other service endpoints are added automatically from configuration
				_wcfHost.AddServiceEndpoint(typeof(IServiceEngine), binding, instance.ServiceUrl);

				// Open the listener
				_wcfHost.Open();
			}

			OnInit();
		}

		/// <summary>
		/// 
		/// </summary>
		protected virtual void OnInit()
		{
		}

		/*=========================*/
		#endregion

		#region Execution
		/*=========================*/

		/// <summary>
		/// 
		/// </summary>
		public void Run()
		{
			// Ignore any run commands when the state is not ready
			if (State != ServiceState.Ready && State != ServiceState.Waiting)
				return;

			// Init the execution timer
			if(_executionTimer == null)
			{
				_executionTimer = new System.Timers.Timer(Instance.Configuration.MaxExecutionTime.TotalMilliseconds);
				_executionTimer.Elapsed += new System.Timers.ElapsedEventHandler(MaxExecutionTimeElapsed);
				_executionTimer.Start();
			}

			ServiceOutcome outcome = ServiceOutcome.Unspecified;

			// Differnet processecing depending on type
			if (Instance.Configuration.ServiceType == ServiceType.Executable)
			{
				// Delegate for reporting success outcome
				_processEndHandler = new EventHandler(delegate(object sender, EventArgs e)
				{
					// This is run outside of this method
					Stop(ServiceOutcome.Success);
				});

				_process = new Process();
				_process.StartInfo.FileName = Instance.Configuration.ProcessPath;
				_process.StartInfo.Arguments = Instance.Configuration.ProcessArguments;
				_process.StartInfo.UseShellExecute = false;
				_process.EnableRaisingEvents = true;
				_process.Exited += _processEndHandler;

				// Start the process - exceptions will be handled by service error handling plumbling
				_process.Start();
				
				//catch (Exception ex)
				//{
				//    // TODO: throw exception instead for infrastructure to catch
				//    throw new Exception("Failed to start external Win32 process.", ex);
				//}

				State = ServiceState.Running;
			}
			else
			{
				// Add other conditions for settings as run
				State = ServiceState.Running;
				try
				{


					outcome = DoWork();
				}
				catch (ThreadAbortException ex)
				{
					Log.Write(ex.Message, ex, LogMessageType.Error);
					throw ex;
					throw;
				}
			}

			// Apply returned outcome
			if (outcome != ServiceOutcome.Unspecified)
			{
				// Stop and report outcome
				Stop(outcome);
			}
			else
			{
				// Change state to waiting till next Run() is called
				State = ServiceState.Waiting;
			}
		}

		/// <summary>
		/// 
		/// </summary>
		protected virtual ServiceOutcome DoWork()
		{
			if (Outcome != ServiceOutcome.Unspecified || State != ServiceState.Running)
			{
				throw new InvalidOperationException("DoWork() cannot be called because the service has ended.");
			}

			// Simulate processing execution time
			if (Instance.Configuration.DebugDelay != TimeSpan.Zero)
				Thread.Sleep(Instance.Configuration.DebugDelay);

			// Service with no steps means instant success!
			if (Instance.Configuration.Workflow.Count < 1)
			{
				return ServiceOutcome.Success;
			}

			// Add the first step of the history as null - indicator for starting
			if (_stepHistory.Count < 1)
				_stepHistory.Add(null);

			// The return result
			bool stepsInProgress = false;

			ServiceOutcome outcome = ServiceOutcome.Unspecified;

			// Loop over each step in the history
			foreach (StepInfo step in _stepHistory.ToArray())
			{
				bool moveToNextStep = true;

				if (step != null)
				{
					stepsInProgress = stepsInProgress || step.ServiceState != ServiceState.Ended;

					// As long as a blocking process is running, halt all processing
					if (stepsInProgress && step.Config.IsBlocking)
						break;

					// Indicates whether to process next step
					moveToNextStep = step.ServiceOutcome == ServiceOutcome.Success;

					if (!step.FailureWasHandled && (step.ServiceOutcome == ServiceOutcome.Failure|| step.ServiceOutcome == ServiceOutcome.Aborted))
					{
						if (step.FailureRepetitions < step.Config.FailureRepeat - 1 && step.ServiceOutcome != ServiceOutcome.Aborted)
						{
							step.ServiceState = ServiceState.Uninitialized;
							step.FailureRepetitions++;
						}
						else
						{
							if (step.Config.FailureOutcome == FailureOutcome.Continue)
							{
								// Process same as Success
								moveToNextStep = true;
							}

							// This here is due to base[] getter bug
							else if
								(
								!step.Config.IsFailureHandler &&
								step.Config.FailureHandler.Element != null &&
								step.Config.FailureHandler.Element.BaseConfiguration.Element.Name != null &&
								!IsInStepHistory(step.Config.FailureHandler.Element) //&&
								//IsStepConditionValid(step.Config.FailureHandler.Element.ConditionOptions, step.Config.FailureHandler.Element.Condition)
								)
							{
								// Get the correct step element
								AccountServiceSettingsElement stepSettings = Instance.Configuration.StepSettings != null ? Instance.Configuration.StepSettings[step.Config.FailureHandler.Element] : null;

								// Add a new step, the failure handler
								if (stepSettings != null)
									_stepHistory.Add(new StepInfo(stepSettings));
								else
									_stepHistory.Add(new StepInfo(step.Config.FailureHandler.Element));

								// Done handling this step's failure
								step.FailureWasHandled = true;
							}
							else
							{
								// Terminate because there is no failure handling - abort and stop processing
								outcome = ServiceOutcome.Failure;
								break;
							}
						}
					}
				}

				if (moveToNextStep)
				{
					// Get rid of the first null used to jump start the processing loop
					if (step == null)
						_stepHistory.Remove(step);

					// Get the next step of a failure handler
					WorkflowStepElement nextStepConfig = GetNextStep(step == null ? null : step.StepConfig);

					if (nextStepConfig == null)
					{
						// No steps left to process
						if (_stepHistory.TrueForAll(new Predicate<StepInfo>(delegate(StepInfo s) { return s.ServiceState == ServiceState.Ended; })))
						{
							// All steps ended - outcome successful
							outcome = ServiceOutcome.Success;
							break;
						}
						else
						{
							// There are still steps being run so wait for them to complete before reporting that we are done
							continue;
						}
					}
					else if (nextStepConfig.WaitForPrevious && stepsInProgress)
					{
						// The next step needs to wait for all previous steps to end - stop processing
						break;
					}
					else
					{
						bool adding = true;
						while (adding)
						{
							if (!IsInStepHistory(nextStepConfig))
							{	
								// Get the correct step element
								AccountServiceSettingsElement stepSettings = Instance.Configuration.StepSettings != null ? Instance.Configuration.StepSettings[nextStepConfig] : null;

								// Schedule the next step as long as it hasn't been added already
								if (stepSettings != null)
									_stepHistory.Add(new StepInfo(stepSettings));
								else
									_stepHistory.Add(new StepInfo(nextStepConfig));
							}

							// If the step is blocking or if it's a failure handler, stop adding steps, otherwise add the next
							if (nextStepConfig.IsBlocking || nextStepConfig == Instance.Configuration.Workflow[Instance.Configuration.Workflow.Count - 1])
							{
								adding = false;
							}
							else
							{
								nextStepConfig = GetNextStep(nextStepConfig);

								// Only add the next if it doesn't require previous steps to end first
								if (nextStepConfig == null || nextStepConfig.WaitForPrevious)
									adding = false;
							}
						}
					}
				}
			}

			if (outcome == ServiceOutcome.Unspecified)
			{
				// Request any pending child steps to be run
				foreach(StepInfo step in _stepHistory)
				{
					if (step.ServiceState == ServiceState.Uninitialized)
						RequestChildService(Instance.Configuration.Workflow.IndexOf(step.StepConfig), step.FailureRepetitions+1);
				}
			}
			
			return outcome;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="nextStepConfig"></param>
		/// <returns></returns>
		bool IsInStepHistory(WorkflowStepElement nextStepConfig)
		{
			bool alreadyAdded = false;
			foreach (StepInfo s in _stepHistory)
			{
				// Check whether the next step hasn't been added already
				if (s.StepConfig == nextStepConfig)
				{
					alreadyAdded = true;
					break;
				}
			}

			return alreadyAdded;
		}

		/// <summary>
		/// 
		/// </summary>
		StepInfo GetStepFromHistory(WorkflowStepElement stepConfig)
		{
			foreach (StepInfo s in _stepHistory)
			{
				// Check whether the next step hasn't been added already
				if (s.StepConfig == stepConfig)
				{
					return s;
				}
			}

			return null;
		}

		/// <summary>
		/// Gets the next step that is enabled and is not a failure handler. If there is none returns null.
		/// </summary>
		WorkflowStepElement GetNextStep(WorkflowStepElement stepConfig)
		{
			int indexOfCurrent = stepConfig == null ? 0 : Instance.Configuration.Workflow.IndexOf(stepConfig) + 1;

			// Find the next non-failure handler step
			WorkflowStepElement next = null;
			for (int i = indexOfCurrent; i < Instance.Configuration.Workflow.Count; i++)
			{
				WorkflowStepElement step = Instance.Configuration.Workflow[i];
				if (
					step.IsEnabled &&
					!step.IsFailureHandler // &&
					//IsStepConditionValid(step.ConditionOptions, step.Condition)
				)
				{
					next = Instance.Configuration.Workflow[i];
					break;
				}
			}

			return next;
		}

		#region OBSOLETE not in used
		/*
		bool IsStepConditionValid(string[] requiredOptions, string expression)
		{
			if (String.IsNullOrEmpty(expression))
				return true;

			foreach (string requiredOption in requiredOptions)
			{
				if (!Instance.Configuration.Options.ContainsKey(requiredOption))
					return true;
			}

			// Resolve characters
			expression = expression.Replace("'", "\"");

			if (_globalConditionVars == null)
			{
				_globalConditionVars = new List<EvaluatorVariable>();
				foreach (KeyValuePair<string, string> option in Instance.Configuration.Options)
				{
					_globalConditionVars.Add(new EvaluatorVariable(option.Key, String.Format("\"{0}\"", option.Value), typeof(string)));
				}
			}

			return Evaluator.Eval<bool>(expression, _globalConditionVars.ToArray());
		}
		*/
		#endregion

		/*=========================*/
		#endregion

		#region Error handling
		/*=========================*/

		/// <summary>
		/// Handles any exception unhandled within the app domain. 
		/// </summary>
		/// <remarks>
		/// WCF has its own exception handling mechanism which bypasses the AppDomain.UnhandledException event.
		/// For unhandled exceptions origination on WCF threads, IErrorHandler.ProvideFault triggers this method
		/// explicitly.
		/// </remarks>
		void UnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			// Mark the outcome as Failed
			_exceptionThrown = true;

			// Log the exception
			Log.Write("Unhandled exception occured.", e.ExceptionObject as Exception);
		}

		/// <summary>
		// Instead of providing fault information, dispathes a 'failed' outcome and lets WCF continue handing the rest	
		/// </summary>
		void IErrorHandler.ProvideFault(Exception error, MessageVersion version, ref Message fault)
		{
			if (OperationContext.Current == null)
			{
				Log.Write("Unhandled exception occured outside of an operation context.", error);
			}
			else if (OperationContext.Current.EndpointDispatcher.ContractName == typeof(IServiceEngine).Name)
			{
				// Simulate an UnhandledException event using the cause of the fault
				UnhandledException(AppDomain.CurrentDomain, new UnhandledExceptionEventArgs(error, false));
				Stop(ServiceOutcome.Unspecified);
			}
			else
			{
				Log.Write(
					String.Format(
						"Unhandled exception occured while performing a request made using the contract {0}.",
						OperationContext.Current.EndpointDispatcher.ContractName),
					error);
			}

			// Returning null resumes automatic error handling
			fault = null;
		}

		/// <summary>
		/// Indicateds the error has been handled and there is no reason to return fault messages.
		/// </summary>
		/// <param name="error"></param>
		/// <returns></returns>
		bool IErrorHandler.HandleError(Exception error)
		{
			Log.Write(error.Message, LogMessageType.Warning);

			if (OperationContext.Current != null &&
			OperationContext.Current.EndpointDispatcher != null &&
			OperationContext.Current.EndpointDispatcher.ContractName != typeof(IServiceEngine).FullName
			)
			{
				// Throw exception
				return false;
			}
			else
			{
				// Ignore exception when it occured within the service
				return true;
			}
		}

		/*=========================*/
		#endregion

		#region Abortion
		/*=========================*/


		void MaxExecutionTimeElapsed(object sender, System.Timers.ElapsedEventArgs e)
		{
			// Abort when time is up
			if (State != ServiceState.Aborting && State != ServiceState.Ended)
			{
				Log.Write(String.Format("Max execution time elapsed, aborting the service. (Running time: {0})", e.SignalTime.ToShortTimeString()), LogMessageType.Error);
				this.Abort();
			}

			_executionTimer.Stop();
		}

		/// <summary>
		/// 
		/// </summary>
		public void Abort()
		{
			if (State == ServiceState.Aborting || State == ServiceState.Ended)
				return;

			State = ServiceState.Aborting;

			// Stop the service
			Stop(ServiceOutcome.Aborted);
		}

		void DomainUnload(object sender, EventArgs e)
		{
			// If we need to stop from here it means an external appdomain called an unload
			if (!_stopped)
			{
				Stop(ServiceOutcome.Unspecified);
			}
		}

		void Stop(ServiceOutcome outcome)
		{
			lock (_stopLock)
			{
				// Enforce only one stop call
				if (_stopped)
					throw new InvalidOperationException("Stop can only be called once.");

				_stopped = true;

				// Kill the timer
				if (_executionTimer != null)
					_executionTimer.Stop();

				// Tell the external process to close
				KillExternalProcess();

				// Indicates that stop was entered during an abort
				bool aborting = State == ServiceState.Aborting;

				// Change state to ended
				State = ServiceState.Ended;

				// Change outcome to whatever was reported
				if (aborting)
				{
					_outcome = ServiceOutcome.Aborted;
				}
				else if (_exceptionThrown)
				{
					_outcome = ServiceOutcome.Failure;
				}
				else
				{
					// Apply outcome
					_outcome = outcome;
					ReportProgress(1.0);
				}

				// Call finalizers
				OnEnded(_outcome);

				// Wait for all log messageds to be written
				Log.StopPump();

				// Notify subscribers
				if (_subscribers.ContainsKey(ServiceEventType.OutcomeReported))
				{
					_subscribers[ServiceEventType.OutcomeReported].ForEach(delegate(IServiceSubscriber subscriber)
					{
						try
						{
							subscriber.OutcomeReported(_outcome);
						
						}
						catch (Exception ex)
						{
							Log.Write("Failed to notify the instance proxy (ServiceInstance object) of an engine event.", ex);
						}
					});
				}

				// Close WCF host				
				if (_wcfHost != null)
				{
					if (_wcfHost.State == CommunicationState.Faulted)
						_wcfHost.Abort();
					else
						_wcfHost.Close();
				}

				// If stop was not called by DomainUnload, unload again
				AppDomain.Unload(AppDomain.CurrentDomain);
			}
		}

		private void KillExternalProcess()
		{
			if (_process != null)
			{
				_process.Exited -= _processEndHandler;
				_process.Kill();
				_process = null;
			}
		}

		/// <summary>
		/// When overriden in a derived class, handles last minute actions after the normal
		/// service execution has ended but before the outcome is reported.
		/// </summary>
		/// <param name="outcome"></param>
		protected virtual void OnEnded(ServiceOutcome outcome)
		{
		}

		/// <summary>
		/// 
		/// </summary>
		void IDisposable.Dispose()
		{
			Stop(ServiceOutcome.Unspecified);
		}


		/*=========================*/
		#endregion

		#region Communication
		/*=========================*/

		void IServiceEngine.Subscribe()
		{
			Subscribe(ServiceEventType.StateChanged);
			Subscribe(ServiceEventType.OutcomeReported);
			Subscribe(ServiceEventType.ProgressReported);
			Subscribe(ServiceEventType.ChildServiceRequested);
		}

		void Subscribe(ServiceEventType eventType)
		{
			List<IServiceSubscriber> subscribers;
			if (!_subscribers.TryGetValue(eventType, out subscribers))
			{
				subscribers = new List<IServiceSubscriber>();
				_subscribers.Add(eventType, subscribers);
			}

			IServiceSubscriber subscriber = OperationContext.Current.GetCallbackChannel<IServiceSubscriber>();
			if (!subscribers.Contains(subscriber))
				subscribers.Add(subscriber);
		}

		void IServiceEngine.Unsubscribe()
		{
			Unsubscribe(ServiceEventType.StateChanged);
			Unsubscribe(ServiceEventType.OutcomeReported);
			Unsubscribe(ServiceEventType.ProgressReported);
			Unsubscribe(ServiceEventType.ChildServiceRequested);
		}

		void Unsubscribe(ServiceEventType eventType)
		{
			List<IServiceSubscriber> subscribers;
			if (!_subscribers.TryGetValue(eventType, out subscribers))
				return; // Nothing to unsubscribe

			IServiceSubscriber subscriber = OperationContext.Current.GetCallbackChannel<IServiceSubscriber>();
			if (subscribers.Contains(subscriber))
				subscribers.Remove(subscriber);
		}

		/// <summary>
		/// 
		/// </summary>
		void IServiceEngine.ChildServiceOutcomeReported(int stepNumber, ServiceOutcome outcome)
		{
			StepInfo step = GetStepFromHistory(Instance.Configuration.Workflow[stepNumber]);
			if (step == null)
				return;

			step.ServiceOutcome = outcome;

			// We want to continue execution when a child has completed
			if (this.State == ServiceState.Waiting)
				((IServiceEngine)this).Run();
		}

		/// <summary>
		/// 
		/// </summary>
		void IServiceEngine.ChildServiceStateChanged(int stepNumber, ServiceState state)
		{
			StepInfo step = GetStepFromHistory(Instance.Configuration.Workflow[stepNumber]);
			if (step == null)
				return;

			step.ServiceState = state;
		}

		/// <summary>
		/// Report progress of children
		/// </summary>
		void IServiceEngine.ChildServiceProgressReported(int stepNumber, double progress)
		{
			StepInfo currentStep = GetStepFromHistory(Instance.Configuration.Workflow[stepNumber]);
			if (currentStep == null)
				return;

			currentStep.Progress = progress;

			double total = 0;
			foreach (StepInfo step in _stepHistory)
			{
				total += (step.ServiceState == ServiceState.Ended ? 1.0 : step.Progress) / _stepHistory.Count;
			}
			if (total > 1.0)
				total = 1.0;

			ReportProgress(total);
		}

		/// <summary>
		/// 
		/// </summary>
		void RequestChildService(int stepNumber)
		{
			RequestChildService(stepNumber, 1);
		}

		/// <summary>
		///
		/// </summary>
		protected virtual void RequestChildService(int stepNumber, int attemptNumber, SettingsCollection options = null)
		{
			List<IServiceSubscriber> subscribers;
			if (!_subscribers.TryGetValue(ServiceEventType.ChildServiceRequested, out subscribers))
				return;

			foreach (IServiceSubscriber subscriber in subscribers)
				subscriber.ChildServiceRequested(stepNumber, attemptNumber, options);
		}

		/// <summary>
		/// 
		/// </summary>
		protected void ReportProgress(double progress)
		{
			if (progress < 0 || progress > 1)
				throw new ArgumentException("Progress should be between 0.0 (started) to 1.0 (done).", "progress");

			List<IServiceSubscriber> subscribers;
			if (!_subscribers.TryGetValue(ServiceEventType.ProgressReported, out subscribers))
				return;

			foreach (IServiceSubscriber subscriber in subscribers)
				subscriber.ProgressReported(progress);
		}

		internal void SyncWorkflowContext(KeyValuePair<string,string>[] set = null, string[] remove = null, bool clear = false)
		{
		}

		/*=========================*/
		#endregion




		public IsAlive IsAlive()
		{
			return IsAlive();
		}


	}

	/// <summary>
	/// Internal class for managing information on pending and completed steps.
	/// </summary>
	internal class StepInfo
	{
		#region Fields
		/*=========================*/
		public readonly ActiveWorkflowStepElement Config;
		public int FailureRepetitions = 0;
		public bool FailureWasHandled = false;
		public readonly WorkflowStepElement StepConfig;
		public readonly AccountServiceSettingsElement AccountStepConfig;
		public ServiceState ServiceState;
		public ServiceOutcome ServiceOutcome;
		public double Progress = 0;

		/*=========================*/
		#endregion

		#region Constructors
		/*=========================*/

		/// <summary>
		/// 
		/// </summary>
		public StepInfo(AccountServiceSettingsElement accountStepConfig)
		{
			StepConfig = accountStepConfig.Step.Element;
			AccountStepConfig = accountStepConfig;
			Config = new ActiveWorkflowStepElement(AccountStepConfig);
		}

		/// <summary>
		/// 
		/// </summary>
		public StepInfo(WorkflowStepElement stepConfig)
		{
			StepConfig = stepConfig;
			Config = new ActiveWorkflowStepElement(StepConfig);
		}

		/*=========================*/
		#endregion
	}

	/// <summary>
	/// Internal class for initializing a service. Used for starting the service engine from a different AppDomain.
	/// </summary>
	internal class ServiceStart: MarshalByRefObject
	{
		#region Constructors
		/*=========================*/

		public ServiceStart(string configurationFileName)
		{
			if (configurationFileName != null)
				EdgeServicesConfiguration.Load(configurationFileName);
		}

		public void Start(ServiceInstanceInfo instance)
		{
			Service s = null;
			string typeName = instance.Configuration.Class;

			if (!String.IsNullOrEmpty(typeName))
			{
				Type serviceType =  Type.GetType(typeName, true);
				if (!serviceType.IsSubclassOf(typeof(Service)))
					throw new TypeLoadException("Service type must derive from Service (Edge.Core.Services).");

				// Enforce private constructors
				ConstructorInfo ctor = serviceType.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, new Type[] { }, null);
				if (ctor == null)
				{
					throw new MissingMethodException("Service requires a non-public parameterless constructor.");
				}

				// This is not suppoted: ctor.IsDefaultConstructor
				//else if (ctor.DeclaringType != typeof(Service) && !ctor.IsDefaultConstructor)
				//{
				//    throw new Exception("Service constructor cannot be public.");
				//}
				
				s = (Service) ctor.Invoke(null);
			}
			else
			{
				s = new Service();
			}

			s.Init(instance);
		}
		
		/*=========================*/
		#endregion
	}

	/// <summary>
	/// Service host that assigns the hosted service as its own error handler.
	/// </summary>
	internal class ServiceEngineHost: ServiceHost
	{
		public ServiceEngineHost(object singletonInstance) : base(singletonInstance)
		{
		}

		protected override void InitializeRuntime()
		{
			base.InitializeRuntime();
			
			// Add the service as its own error handler
			if (this.SingletonInstance is IErrorHandler)
			{
				foreach (ChannelDispatcher channelDispatcher in this.ChannelDispatchers)
					channelDispatcher.ErrorHandlers.Add(this.SingletonInstance as IErrorHandler);
			}
		}
	}

    
}
