using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Security.Permissions;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Threading;
using System.Xml.Serialization;
using Edge.Core.Configuration;
using Edge.Core.Data;
using Edge.Core.Utilities;
using Eggplant.Persistence;

namespace Edge.Core.Services
{
	/// <summary>
	/// Core class of the service-oriented architecture.
	/// </summary>
	[CallbackBehavior(UseSynchronizationContext=false, ConcurrencyMode=ConcurrencyMode.Single)]
	public class ServiceInstance: Entity, IServiceInstance, IServiceSubscriber, IDisposable
	{
		#region Fields
		/*=========================*/

		ActiveServiceElement _config;
		SchedulingRuleElement _activeRule;
		
		EventHandler<ServiceStateChangedEventArgs> _childStateHandler;
		EventHandler _childOutcomeHandler;
		EventHandler _childProgressHandler;

		AppDomain _appDomain;
		ServiceEngineCommChannel _commChannel;

		Dictionary<ServiceInstance, int> _childServices = new Dictionary<ServiceInstance, int>();
		Log Log;

		/*=========================*/
		#endregion

		#region Events
		/*=========================*/
		
		public event EventHandler<ServiceStateChangedEventArgs> StateChanged;
		public event EventHandler OutcomeReported;
		public event EventHandler ProgressReported;
		public event EventHandler<ServiceRequestedEventArgs> ChildServiceRequested;

		/*=========================*/
		#endregion

		#region Constructor
		/*=========================*/
		
		/// <summary>
		/// 
		/// </summary>
		internal ServiceInstance(ActiveServiceElement activeConfiguration, ServiceInstance parentInstance, int accountID)
		{
			ActiveConfigurationProperty.SetValue(this, activeConfiguration);
			ParentInstanceProperty.SetValue(this, parentInstance);

			if (accountID > -1)
				AccountIDProperty.SetValue(this, accountID);

			_childStateHandler = new EventHandler<ServiceStateChangedEventArgs>(ChildStateChanged);
			_childOutcomeHandler = new EventHandler(ChildOutcomeReported);
			_childProgressHandler = new EventHandler(ChildProgressReported);

			Log = new Log(this);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="contents"></param>
		private ServiceInstance(DataRow contents)
		{
		}

		/*=========================*/
		#endregion

		#region Properties
		/*=========================*/

		public int AccountID
		{
			get { return AccountIDProperty.GetValue(this); }
		}

		/// <summary>
		/// 
		/// </summary>
		public long InstanceID
		{
			get { return InstanceIDProperty.GetValue(this); }
		}

		/// <summary>
		/// 
		/// </summary>
		public string ServiceUrl
		{
			get { return ServiceUrlProperty.GetValue(this); }
		}

		/// <summary>
		/// 
		/// </summary>
		public ServiceState State
		{
			get { return StateProperty.GetValue(this); }
			private set { OnStateChanged(value); }
		}

		/// <summary>
		/// 
		/// </summary>
		public ServiceOutcome Outcome
		{
			get { return OutcomeProperty.GetValue(this); }
		}

		/// <summary>
		/// Value between 0.0 (just started) and 1.0 (complete).
		/// </summary>
		public double Progress
		{
			get { return ProgressProperty.GetValue(this); }
		}

		/// <summary>
		/// Gets the parent service of the this service, i.e. the service that requested this one to be run.
		/// </summary>
		public ServiceInstance ParentInstance
		{
			get { return ParentInstanceProperty.GetValue(this); }
		}

		/// <summary>
		/// 
		/// </summary>
		public DateTime TimeStarted
		{
			get { return TimeStartedProperty.GetValue(this); }
		}

		/// <summary>
		/// 
		/// </summary>
		public DateTime TimeEnded
		{
			get { return TimeEndedProperty.GetValue(this); }
		}

		/// <summary>
		/// 
		/// </summary>
		public DateTime TimeScheduled
		{
			get { return TimeScheduledProperty.GetValue(this); }
			set { TimeScheduledProperty.SetValue(this, value); }
		}

		/// <summary>
		/// 
		/// </summary>
		public ActiveServiceElement Configuration
		{
			get { return ActiveConfigurationProperty.GetValue(this); }
		}

		/// <summary>
		/// 
		/// </summary>
		public SchedulingRuleElement ActiveSchedulingRule
		{
			get { return ActiveRuleProperty.GetValue(this); }
			set { ActiveRuleProperty.SetValue(this, value); }
		}

		/// <summary>
		/// 
		/// </summary>
		public ServicePriority Priority
		{
			get { return PriorityProperty.GetValue(this); }
			set { PriorityProperty.SetValue(this, value); }
		}

		ServiceInstanceInfo IServiceInstance.ParentInstance
		{
			get
			{
				if (this.ParentInstance != null)
					return new ServiceInstanceInfo(this.ParentInstance);
				else
					return null;
			}
		}

		public override string ToString()
		{
			return String.Format("{0} (ID {1}, account {2})", this.Configuration.Name, this.InstanceID, this.AccountID);
		}

		/*=========================*/
		#endregion

		#region Entity Implementation
		/*=========================*/

		static readonly EntityProperty<ServiceInstance, int> AccountIDProperty = new EntityProperty<ServiceInstance, int>(-1);
		static readonly EntityProperty<ServiceInstance, long> InstanceIDProperty = new EntityProperty<ServiceInstance, long>(-1);
		static readonly EntityProperty<ServiceInstance, string> ServiceUrlProperty = new EntityProperty<ServiceInstance, string>();
		static readonly EntityReferenceProperty<ServiceInstance, ServiceInstance, long> ParentInstanceProperty = new EntityReferenceProperty<ServiceInstance,ServiceInstance,long>(ServiceInstance.InstanceIDProperty);
		static readonly EntityProperty<ServiceInstance, ServicePriority> PriorityProperty = new EntityProperty<ServiceInstance, ServicePriority>(ServicePriority.Normal);
		static readonly EntityProperty<ServiceInstance, ServiceState> StateProperty = new EntityProperty<ServiceInstance, ServiceState>(ServiceState.Uninitialized);
		static readonly EntityProperty<ServiceInstance, ServiceOutcome> OutcomeProperty = new EntityProperty<ServiceInstance, ServiceOutcome>(ServiceOutcome.Unspecified);
		static readonly EntityProperty<ServiceInstance, double> ProgressProperty = new EntityProperty<ServiceInstance, double>(0);
		static readonly EntityProperty<ServiceInstance, DateTime> TimeStartedProperty = new EntityProperty<ServiceInstance, DateTime>(DateTime.MinValue);
		static readonly EntityProperty<ServiceInstance, DateTime> TimeEndedProperty = new EntityProperty<ServiceInstance, DateTime>(DateTime.MinValue);
		static readonly EntityProperty<ServiceInstance, DateTime> TimeScheduledProperty = new EntityProperty<ServiceInstance, DateTime>(DateTime.MinValue);
		static readonly EntityProperty<ServiceInstance, ActiveServiceElement> ActiveConfigurationProperty = new EntityProperty<ServiceInstance, ActiveServiceElement>();
		static readonly EntityProperty<ServiceInstance, SchedulingRuleElement> ActiveRuleProperty = new EntityProperty<ServiceInstance, SchedulingRuleElement>();

		static ServiceInstance()
		{
			ActiveConfigurationProperty.Getting += new EventHandler<ValueTranslationEventArgs>(ActiveConfigurationProperty_Getting);
			ActiveConfigurationProperty.Setting += new EventHandler<ValueTranslationEventArgs>(ActiveConfigurationProperty_Setting);
			ActiveRuleProperty.Getting += new EventHandler<ValueTranslationEventArgs>(ActiveRuleProperty_Getting);
			ActiveRuleProperty.Setting += new EventHandler<ValueTranslationEventArgs>(ActiveRuleProperty_Setting);
			ParentInstanceProperty.ReferenceEntityRequired += new EventHandler<ValueTranslationEventArgs>(ParentInstanceProperty_ReferenceEntityRequired);
			TimeStartedProperty.Setting += new EventHandler<ValueTranslationEventArgs>(TimeStartedProperty_Setting);
			StateProperty.Setting += new EventHandler<ValueTranslationEventArgs>(StateProperty_Setting);
		}

		static void StateProperty_Setting(object sender, ValueTranslationEventArgs e)
		{
		}

		static void ActiveConfigurationProperty_Getting(object sender, ValueTranslationEventArgs e)
		{
			ServiceInstance current = (ServiceInstance) e.Entity;
			e.Output = current._config;
		}

		static void ActiveConfigurationProperty_Setting(object sender, ValueTranslationEventArgs e)
		{
			ServiceInstance current = (ServiceInstance) e.Entity;
			if ((int) current.State > (int) ServiceState.Uninitialized)
				throw new InvalidOperationException("Cannot change properties after Initialize has been called.");

			current._config = (ActiveServiceElement) e.Input;
			e.Output = null;
		}

		static void ActiveRuleProperty_Getting(object sender, ValueTranslationEventArgs e)
		{
			ServiceInstance current = (ServiceInstance) e.Entity;
			e.Output = current._activeRule;
		}

		static void ActiveRuleProperty_Setting(object sender, ValueTranslationEventArgs e)
		{
			ServiceInstance current = (ServiceInstance) e.Entity;
			if ((int)current.State > (int) ServiceState.Uninitialized)
				throw new InvalidOperationException("Cannot change properties after Initialize has been called.");

			current._activeRule = (SchedulingRuleElement) e.Input;
			e.Output = null;
		}

		static void ParentInstanceProperty_ReferenceEntityRequired(object sender, ValueTranslationEventArgs e)
		{
			throw new NotImplementedException("There is currently no way to retrieve the parent ServiceInstance by ID only.");
		}

		static void TimeStartedProperty_Setting(object sender, ValueTranslationEventArgs e)
		{
			if (TimeStartedProperty.GetValue((ServiceInstance) e.Entity) > DateTime.MinValue)
				e.Cancel = true;
		}

		[Obsolete("Temporary method of saving, should be changed to use Persistence model")]
		public new void Save()
		{
			bool isInsert = this.InstanceID < 0;
			string cmdText = isInsert ?
				@"
				insert into ServiceInstance (AccountID, ParentInstanceID, ServiceName, TimeScheduled, TimeStarted, TimeEnded, Priority, State, Progress, Outcome, ServiceUrl, Configuration, ActiveRule)
				values (@accountID:Int, @parentInstanceID:BigInt, @serviceName:NVarChar, @timeScheduled:DateTime, @timeStarted:DateTime,@timeEnded:DateTime, @priority:Int, @state:Int, @progress:Float, @outcome:Int, @serviceUrl:NVarChar, @configuration:Xml, @activeRule:Xml);
				select scope_identity();
				" :
				this.State == ServiceState.Uninitialized || this.State == ServiceState.Initializing ? 
					@"
					update ServiceInstance set
						ServiceName = @serviceName:NVarChar,
						TimeScheduled = @timeScheduled:DateTime,
						TimeStarted = @timeStarted:DateTime,
						TimeEnded = @timeEnded:DateTime,
						Priority = @priority:Int,
						State = @state:Int,
						Progress = @progress:Float,
						Outcome = @outcome:Int,
						ServiceUrl = @serviceUrl:NVarChar,
						Configuration = @configuration:Xml,
						ActiveRule = @activeRule:Xml
					where
						InstanceID = @instanceID:BigInt
					" : 
					@"
					update ServiceInstance set
						TimeStarted = @timeStarted:DateTime,
						TimeEnded = @timeEnded:DateTime,
						State = @state:Int,
						Progress = @progress:Float,
						Outcome = @outcome:Int,
						ServiceUrl = @serviceUrl:NVarChar
					where
						InstanceID = @instanceID:BigInt
					"
			  ;

			SqlCommand cmd = DataManager.CreateCommand(cmdText);

			// Always set these
			cmd.Parameters["@state"].Value = this.State;
			cmd.Parameters["@progress"].Value = this.Progress;
			cmd.Parameters["@outcome"].Value = this.Outcome;
			cmd.Parameters["@timeStarted"].Value = this.TimeStarted == DateTime.MinValue ? (object)DBNull.Value : (object)this.TimeStarted;
			cmd.Parameters["@timeEnded"].Value = this.TimeEnded == DateTime.MinValue ? (object)DBNull.Value : (object)this.TimeEnded;
			cmd.Parameters["@serviceUrl"].Value = this.ServiceUrl == null ? (object)DBNull.Value : (object)this.ServiceUrl;

			// Set only when uninitialized
			if (this.State == ServiceState.Uninitialized || this.State == ServiceState.Initializing)
			{
				cmd.Parameters["@serviceName"].Value = Configuration.Name;
				cmd.Parameters["@timeScheduled"].Value = this.TimeScheduled == DateTime.MinValue ? (object) DBNull.Value : (object) this.TimeScheduled;
				cmd.Parameters["@priority"].Value = this.Priority;
				cmd.Parameters["@configuration"].Value = this.Configuration.GetXml();
				cmd.Parameters["@activeRule"].Value = this.ActiveSchedulingRule == null ?
				(object) DBNull.Value : 
				(object) this.ActiveSchedulingRule.GetXml();
			}

			if (isInsert)
			{
				cmd.Parameters["@accountID"].Value = this.AccountID;
				cmd.Parameters["@parentInstanceID"].Value = this.ParentInstance == null ? 
					(object) DBNull.Value : 
					(object) this.ParentInstance.InstanceID;
			}
			else
			{
				cmd.Parameters["@instanceID"].Value = this.InstanceID;
			}

            const int maxTries = 2;
            int tries = 0;
            while (tries < maxTries)
            {
                try
                {
			using (SqlConnection cn = new SqlConnection(AppSettings.GetConnectionString("Edge.Core.Services", "SystemDatabase", configFile: EdgeServicesConfiguration.Current.ConfigurationFile)))
			{
				cmd.Connection = cn;
				cn.Open();
				object newID;

				if (isInsert)
				{
					newID = cmd.ExecuteScalar();
					if (newID is DBNull)
						throw new Exception("Save failed to return a new InstanceID.");
                            else
                            {
                                InstanceIDProperty.SetValue(this, Convert.ToInt64(newID));
                                break;
                            }
                        }
				else
				{
					if (cmd.ExecuteNonQuery() < 1)
						throw new Exception("Save did not affect any rows.");
                            else
                                break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    tries++;
                    if (tries == maxTries)
                    {
                        Log.Write(
							String.Format("{0} ({1})", this.Configuration.Name, this.InstanceID),
							"Failed to save ServiceInstance.", ex);

                        if (this.InstanceID < 0)
                        {
                            OnStateChanged(ServiceState.Ended, false);
                            OnOutcomeReported(ServiceOutcome.Failure, false);
                        }
                    }
                }
            }


		}

		protected override void OnBeforeSave()
		{
			throw new NotImplementedException("Entity.Save not implemented in this version. Use ServiceInstance.Save instead.");
		}

		/*=========================*/
		#endregion

		#region Generate
		/*=========================*/

		/// <summary>
		/// 
		/// </summary>
		internal static ServiceInstance Generate(EnabledConfigurationElement config, ServiceInstance parentInstance, int accountID)
		{
			ActiveServiceElement activeConfig;

			// Get the right constructor
			if (config is ServiceElement)
				activeConfig = new ActiveServiceElement((ServiceElement) config);
			else if (config is WorkflowStepElement)
				activeConfig = new ActiveServiceElement((WorkflowStepElement) config);
			else if (config is AccountServiceElement)
				activeConfig = new ActiveServiceElement((AccountServiceElement) config);
			else if (config is AccountServiceSettingsElement)
				activeConfig = new ActiveServiceElement((AccountServiceSettingsElement) config);
			else
				throw new ArgumentException("Configuration is an unrecognized class.", "config");

			// Don't allow non-enabled services
			if (!activeConfig.IsEnabled)
				throw new InvalidOperationException(String.Format("Cannot create an instance of a disabled service. Set IsEnabled to true in the configuration file in order to run. Service name: {0}", activeConfig.Name));

			return new ServiceInstance(activeConfig, parentInstance, accountID);
		}

		/*=========================*/
		#endregion

		#region Setup
		/*=========================*/

		public void Initialize()
		{
			// EXCEPTION:
			if (State != ServiceState.Uninitialized)
				throw new InvalidOperationException("Service can only be initialized once per lifetime.");

			// Change state to initializing - will invoke save, thus getting a new instanceID
			State = ServiceState.Initializing;

			// Get the service URL based on the instance ID6
			if (this.ServiceUrl == null)
			{
				string baseUrl = AppSettings.Get(typeof(Service), "BaseListeningUrl");
				ServiceUrlProperty.SetValue(this, String.Format(baseUrl, this.InstanceID));
			}
			
			AppDomainSetup setup = new AppDomainSetup();
			setup.ApplicationBase = Directory.GetCurrentDirectory();

			// Load the AppDomain in a different thread
			Action loadAppDomain = new Action(delegate()
			{
				try
				{
					_appDomain = AppDomain.CreateDomain(this.ToString(), null, setup);
				}
				catch (Exception ex)
				{
					// Report failure
					State = ServiceState.Ended;
					OnOutcomeReported(ServiceOutcome.Failure);

					// EXCEPTION:
						Log.Write(
							String.Format("{0} ({1})", this.Configuration.Name, this.InstanceID),
							"Failed to create a new AppDomain for the service.",
							ex);
                        return;
				}
			});

			// Once the app domain loading create the instance
			loadAppDomain.BeginInvoke(new AsyncCallback(delegate(IAsyncResult result)
			{
				try
				{
					ServiceStart start = (ServiceStart) _appDomain.CreateInstanceAndUnwrap(
						typeof(ServiceStart).Assembly.FullName,
						typeof(ServiceStart).FullName,
						false,
						BindingFlags.Default,
						null,
						new object[] { EdgeServicesConfiguration.CurrentFileName },
						null,
						null
						);
					
					// cross-domain invoke
					start.Start(new ServiceInstanceInfo(this));
				}
				catch (Exception ex)
				{
					// Unload app domain because we can't use it anymore
					AppDomain.Unload(_appDomain);
						
					// Report failure
					State = ServiceState.Ended;
					OnOutcomeReported(ServiceOutcome.Failure);

					// EXCEPTION:
						Log.Write(
							String.Format("{0} ({1})", this.Configuration.Name, this.InstanceID),
							"Failed to initialize the service",
							ex);
                        return;
				}

				// Try to open it again now that the service is running
				OpenChannelAndSubscribe();
			}
			),null);
		}

		/// <summary>
		/// 
		/// </summary>
		public void Start()
		{
			// Make sure the underlying service is available
			ThrowIfServiceUnavailable();
	
			// EXCEPTION:
			if (State != ServiceState.Ready)
				throw new InvalidOperationException("Service can only be started when it has reached a Ready state.");

			// Change set to starting
			State = ServiceState.Starting;

			// Mark the time we started (this is only set once)
			TimeStartedProperty.SetValue(this, DateTime.Now);

			// Start the engine
			_commChannel.Engine.Run();
		}

		/// <summary>
		/// 
		/// </summary>
		public void Continue()
		{
			ThrowIfServiceUnavailable();

			// EXCEPTION:
			if (State != ServiceState.Waiting)
				throw new InvalidOperationException("Service can only be continued when State is Waiting.");

			_commChannel.Engine.Run();
		}

		/// <summary>
		/// 
		/// </summary>
		public void Abort()
		{
			ThrowIfServiceUnavailable();
			_commChannel.Engine.Abort();
		}

		/*=========================*/
		#endregion

		#region Communication
		/*=========================*/

		[DebuggerNonUserCode]
		void OpenChannelAndSubscribe()
		{
			// Open the communication channel
			_commChannel = new ServiceEngineCommChannel(this);
			_commChannel.Open();

			// Subscribe to the engine's notifications
			_commChannel.Engine.Subscribe();

			// Change state to ready
			State = ServiceState.Ready;
		}

		void IServiceSubscriber.StateChanged(ServiceState state)
		{
			OnStateChanged(state);
		}

		private void OnStateChanged(ServiceState state, bool save = true)
		{
			ServiceState before = this.State;
			StateProperty.SetValue(this, state);
			if (state == ServiceState.Ended)
				TimeEndedProperty.SetValue(this, DateTime.Now);

            if (save)
			    this.Save();

			if (this.StateChanged != null)
				StateChanged(this, new ServiceStateChangedEventArgs(before, state));

			// Abort child services that are still running
			if (state == ServiceState.Aborting)
			{
				foreach (ServiceInstance instance in _childServices.Keys)
				{
					if (instance.State != ServiceState.Aborting && instance.State != ServiceState.Ended)
						instance.Abort();
				}
			}
		}

		void IServiceSubscriber.OutcomeReported(ServiceOutcome outcome)
		{
			OnOutcomeReported(outcome);
		}

        void OnOutcomeReported(ServiceOutcome outcome, bool save = true)
		{
			OutcomeProperty.SetValue(this, outcome);

            if (save)
			    this.Save();

			if (this.OutcomeReported != null)
				OutcomeReported(this, EventArgs.Empty);

			// Service is done, so unsubscribe to the engine's events
			if (_commChannel != null)
			{
				if (_commChannel.State == CommunicationState.Opened)
					_commChannel.Engine.Unsubscribe();
				else
					_commChannel.Abort();
			}

			/*
			// Remove event handlers from child services
			foreach (ServiceInstance child in _childServices.Keys)
			{
			    StateChanged -= _childStateHandler;
			    OutcomeReported -= _childOutcomeHandler;
			}
			*/
		}

		void IServiceSubscriber.ChildServiceRequested(int stepNumber, int attemptNumber, SettingsCollection options)
		{
			OnChildServiceRequested(stepNumber, attemptNumber, options);
		}

		void OnChildServiceRequested(int stepNumber, int attemptNumber, SettingsCollection options)
		{
			// Get the step configuration elements
			WorkflowStepElement stepConfig = this.Configuration.Workflow[stepNumber];
			AccountServiceSettingsElement stepSettings = 
				this.Configuration.StepSettings != null ? 
				this.Configuration.StepSettings[stepConfig] : 
				null;

			// Take the step configuration
			ActiveServiceElement configuration = stepSettings != null ?
				new ActiveServiceElement(stepSettings) :
				new ActiveServiceElement(stepConfig);

			// Add child-specific options
			if (options != null)
				configuration.Options.Merge(options);

			// Add parent options, without overriding child options with the same name
			foreach (var parentOption in this.Configuration.Options)
			{
				if (!configuration.Options.ContainsKey(parentOption.Key))
					configuration.Options.Add(parentOption.Key, parentOption.Value);
			}

			// Generate a child instance
			ServiceInstance child = Service.CreateInstance(configuration, this, this.AccountID);

			child.StateChanged += _childStateHandler;
			child.OutcomeReported += _childOutcomeHandler;
			child.ProgressReported += _childProgressHandler;
			_childServices.Add(child, stepNumber);

			if (ChildServiceRequested != null)
				ChildServiceRequested(this, new ServiceRequestedEventArgs(child, attemptNumber));
		}
		
		void IServiceSubscriber.ProgressReported(double progress)
		{
			OnProgressReported(progress);
		}

		void OnProgressReported(double progress)
		{
			double before = progress;
			ProgressProperty.SetValue(this, progress);
			this.Save();

			if (ProgressReported != null)
				ProgressReported(this, EventArgs.Empty);

		
		}

		void ChildProgressReported(object sender, EventArgs e)
		{
			ServiceInstance child = (ServiceInstance) sender;
			int stepNumer = _childServices[child];

			if (_commChannel != null && _commChannel.State == CommunicationState.Opened)
				_commChannel.Engine.ChildServiceProgressReported(stepNumer, child.Progress);
		}

		void ChildOutcomeReported(object sender, EventArgs e)
		{
			ServiceInstance child = (ServiceInstance) sender;

			child.StateChanged -= _childStateHandler;
			child.OutcomeReported -= _childOutcomeHandler;
			child.ProgressReported -= _childProgressHandler;

			int stepNumer = _childServices[child];
			_childServices.Remove(child);

			if (_commChannel != null && _commChannel.State == CommunicationState.Opened)
				_commChannel.Engine.ChildServiceOutcomeReported(stepNumer, child.Outcome);
		}

		void ChildStateChanged(object sender, ServiceStateChangedEventArgs e)
		{
			ServiceInstance child = (ServiceInstance) sender;

			if (_commChannel != null && _commChannel.State == CommunicationState.Opened)
				_commChannel.Engine.ChildServiceStateChanged(_childServices[child], e.StateAfter);
		}

		void ThrowIfServiceUnavailable()
		{
			if (_commChannel == null || _commChannel.State != CommunicationState.Opened)
				throw new CommunicationException("The underlying WCF service is unavailable at this point.");
		}


		/*=========================*/
		#endregion

		#region IDisposable Members

		public void Dispose()
		{
			if (_commChannel != null && _commChannel.State == CommunicationState.Opened)
				_commChannel.Close();

			// Close the channel if it is still open
			if (_commChannel != null)
			{
				if (_commChannel.State == CommunicationState.Opened)
					_commChannel.Close();
				else if (_commChannel.State != CommunicationState.Closed)
					_commChannel.Abort();
			}
		}

		#endregion
	}

	/// <summary>
	/// Contains read-only service instance info.
	/// </summary>
	[Serializable]
	public class ServiceInstanceInfo: IServiceInstance
	{
		#region Fields
		/*=========================*/

		int _accountID;
		long _instanceID;
		string _serviceUrl;
		string _configFileName;
		ServicePriority _priority;
		ActiveServiceElement _config = null;
		SchedulingRuleElement _rule = null;
		DateTime _timeStarted;
		DateTime _timeScheduled;
		ServiceInstanceInfo _parentInstanceData = null;

		/*=========================*/
		#endregion

		#region Constructor
		/*=========================*/

		private ServiceInstanceInfo()
		{
		}

		public ServiceInstanceInfo(ServiceInstance instance)
		{
			_accountID = instance.AccountID;
			_instanceID = instance.InstanceID;
			_priority = instance.Priority;
			_config = instance.Configuration;
			_configFileName = EdgeServicesConfiguration.CurrentFileName;
			_rule = instance.ActiveSchedulingRule;
			_timeStarted = instance.TimeStarted;
			_timeScheduled = instance.TimeScheduled;
			_serviceUrl = instance.ServiceUrl;

			if (instance.ParentInstance != null)
				_parentInstanceData = new ServiceInstanceInfo(instance.ParentInstance);
		}

		/*=========================*/
		#endregion

		#region Properties
		/*=========================*/

		public int AccountID
		{
			get { return _accountID; }
		}

		public long InstanceID
		{
			get { return _instanceID; }
		}

		public string ServiceUrl
		{
			get { return _serviceUrl; }
		}

		public string ConfigurationFile
		{
			get { return _configFileName; }
		}

		public ServicePriority Priority
		{
			get { return _priority; }
		}

		public ServiceInstanceInfo ParentInstance
		{
			get { return _parentInstanceData; }
		}

		public ActiveServiceElement Configuration
		{
			get { return _config; }
		}

		public SchedulingRuleElement ActiveSchedulingRule
		{
			get { return _rule; }
		}

		public DateTime TimeStarted
		{
			get { return _timeStarted; }
		}

		public DateTime TimeScheduled
		{
			get { return _timeScheduled; }
		}

		public override string ToString()
		{
			return this.InstanceID < 0 ?
				this.Configuration.Name :
				String.Format("{0} ({1})", this.Configuration.Name, this.InstanceID);
		}
		
		/*=========================*/
		#endregion
	}

	/// <summary>
	/// Duplex client for instance-to-engine communication.
	/// </summary>
	internal class ServiceEngineCommChannel: DuplexClientBase<IServiceEngine>
	{
		#region Constructor
		/*=========================*/

		public ServiceEngineCommChannel(ServiceInstance subscriber)
			: base(
				new InstanceContext(subscriber),
				GetBinding(subscriber),
				new EndpointAddress(subscriber.ServiceUrl)
				)
		{
		}

		/*=========================*/
		#endregion

		#region Properties
		/*=========================*/

		public IServiceEngine Engine
		{
			get { return base.Channel; }
		}

		/*=========================*/
		#endregion

		#region Static Methods
		/*=========================*/

		public static Binding GetBinding(IServiceInstance instance)
		{
			string typeName = AppSettings.Get(typeof(Service), "DefaultWcfBindingType", false);
			Type defaultBindingType = typeName == null ? null : Type.GetType(typeName, false, true);
			ConstructorInfo constructor = defaultBindingType == null ? null : defaultBindingType.GetConstructor(new Type[] { typeof(string) });

			Binding binding = constructor != null ?
						(Binding)constructor.Invoke(new object[] { "Edge.Core.Services.Service.InstanceToEngineBinding" }) :
						(Binding)new NetTcpBinding("Edge.Core.Services.Service.InstanceToEngineBinding");

			// Enable port sharing
			if (binding is NetTcpBinding)
			{
				NetTcpBinding tcpBinding = ((NetTcpBinding) binding);
				if (!tcpBinding.PortSharingEnabled)
				{
					string msg = "Port sharing is required for NetTcpBinding-based communication; turning on PortSharingEnabled.";
					if (Service.Current == null)
						Log.Write(typeof(ServiceEngineCommChannel).Name, msg, LogMessageType.Information);
					else
						Log.Write(typeof(ServiceEngineCommChannel).Name + " - " + msg, LogMessageType.Information);

					tcpBinding.PortSharingEnabled = true;
				}
			}

			// Make sure the receive timeout is always at least 5 minutes longer than the max execution time
			if (instance.Configuration.MaxExecutionTime > binding.ReceiveTimeout.Subtract(TimeSpan.FromMinutes(5)))
			{
				//string msg = "The binding's ReceiveTimeout must be at least 5 minutes longer than the service's MaxExecutionTime; increasing it to meet this requirement.";
				//if (Service.Current == null)
				//    Log.Write(typeof(ServiceEngineCommChannel).Name, msg, LogMessageType.Warning);
				//else
				//    Log.Write(typeof(ServiceEngineCommChannel).Name + " - " + msg, LogMessageType.Warning);

				binding.ReceiveTimeout = instance.Configuration.MaxExecutionTime.Add(TimeSpan.FromMinutes(5));
			}

			return binding;
		}

		/*=========================*/
		#endregion
	}
}
