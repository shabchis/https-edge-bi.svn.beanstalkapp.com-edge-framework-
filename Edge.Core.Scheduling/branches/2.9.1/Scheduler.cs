using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Core;
using Edge.Core.Data;
using System.Data.SqlClient;
using Edge.Core.Scheduling.Objects;
using System.Threading;
using Edge.Core.Configuration;
using Legacy = Edge.Core.Services;
using Edge.Core.Utilities;
using System.Configuration;
using System.IO;





namespace Edge.Core.Scheduling
{
	/// <summary>
	/// The new scheduler
	/// </summary>
	public class Scheduler
	{
		#region members
		private SchedulerState _state;
		private List<string> _tempCompletedServices;
		private Dictionary<int, Profile> _profiles = new Dictionary<int, Profile>();
		private Dictionary<string, ServiceConfiguration> _serviceBaseConfigurations = new Dictionary<string, ServiceConfiguration>();
		private List<ServiceConfiguration> _serviceConfigurationsToSchedule = new List<ServiceConfiguration>(); //all services from configuration file load to this var		
		//private Dictionary<SchedulingRequest, ServiceInstance> _scheduledServices = new Dictionary<SchedulingRequest, ServiceInstance>();
		private ScheduledServices _scheduledServices = new ScheduledServices();
		private Dictionary<string, ServicePerProfileAvgExecutionTimeCash> _servicePerProfileAvgExecutionTimeCash = new Dictionary<string, ServicePerProfileAvgExecutionTimeCash>();
		DateTime _timeLineFrom;
		DateTime _timeLineTo;
		private TimeSpan _neededScheduleTimeLine; //scheduling for the next xxx min....
		private int _percentile = 80; //execution time of specifc service on sprcific Percentile
		private TimeSpan _intervalBetweenNewSchedule;
		private TimeSpan _findServicesToRunInterval;
		private Thread _findRequiredServicesthread;
		private TimeSpan _timeToDeleteServiceFromTimeLine;
		private Thread _newSchedulethread;
		public event EventHandler<ServicesToRunEventArgs> ServiceRunRequiredEvent;
		public event EventHandler<SchedulingInformationEventArgs> NewScheduleCreatedEvent;
		private volatile bool _needReschedule = false;
		private TimeSpan _executionTimeCashTimeOutAfter;
		private object _sync;
		private bool _started = false;

		#endregion

		#region Properties
		public ScheduledServices ScheduledServices
		{
			get { return _scheduledServices; }
		}
		public IQueryable<ServiceConfiguration> ServiceConfigurations
		{
			get { return _serviceConfigurationsToSchedule.AsQueryable(); }
		}
		public SchedulerState SchedulerState
		{
			get { return _state; }
		}

		#endregion

		#region ManageScheduler
		/// <summary>
		/// Initialize all the services from configuration file or db4o
		/// </summary>
		/// <param name="getServicesFromConfigFile"></param>
		public Scheduler(bool getServicesFromConfigFile)
		{
			if (getServicesFromConfigFile)
				LoadServicesFromConfigurationFile();

			_state = new SchedulerState();
			_state.Load();
			_percentile = int.Parse(AppSettings.Get(this, "Percentile"));
			_neededScheduleTimeLine = TimeSpan.Parse(AppSettings.Get(this, "NeededScheduleTimeLine"));
			_intervalBetweenNewSchedule = TimeSpan.Parse(AppSettings.Get(this, "IntervalBetweenNewSchedule"));
			_findServicesToRunInterval = TimeSpan.Parse(AppSettings.Get(this, "FindServicesToRunInterval"));
			_timeToDeleteServiceFromTimeLine = TimeSpan.Parse(AppSettings.Get(this, "DeleteEndedServiceInterval"));
			_executionTimeCashTimeOutAfter = TimeSpan.Parse(AppSettings.Get(this, "DeleteEndedServiceInterval"));
			_sync = new object();
		}

		/// <summary>
		/// start the timers of new scheduling and services required to run
		/// </summary>
		public void Start()
		{
			if (_started)
				return;

			_started = true;
			Schedule(false);
			//NotifyServicesToRun();
			_newSchedulethread = new Thread(new ThreadStart(delegate()
			{
				/* 1) every 5 seconds check if atleast 1 service finished: 
				  if yes run schedule again and update next schedule to 10 min
				  if not minus 5 seconds from the interval of newschedule
				 */

				_tempCompletedServices = new List<string>();

				TimeSpan calcTimeInterval = _intervalBetweenNewSchedule;
				while (true)
				{

					Thread.Sleep(TimeSpan.FromSeconds(5));
					if (_tempCompletedServices.Count > 0)
					{
						Schedule(false);
						lock (_tempCompletedServices)
						{
							_tempCompletedServices.Clear();
						}

						calcTimeInterval = _intervalBetweenNewSchedule;

					}
					else
					{
						calcTimeInterval = calcTimeInterval.Subtract(TimeSpan.FromSeconds(5));
					}

					if (calcTimeInterval == TimeSpan.Zero)
					{
						Schedule(false);
						calcTimeInterval = _intervalBetweenNewSchedule;
					}
				}
			}
			));

			_findRequiredServicesthread = new Thread(new ThreadStart(delegate()
			{
				while (true)
				{
					Thread.Sleep(_findServicesToRunInterval);//TODO: ADD CONST

					if (_needReschedule)
						Schedule(true);

					NotifyServicesToRun();
				}
			}));


			_newSchedulethread.IsBackground = true;
			_newSchedulethread.Start();
			_findRequiredServicesthread.IsBackground = true;
			_findRequiredServicesthread.Start();

		}

		/// <summary>
		///  stop the timers of new scheduling and services required to run
		/// </summary>
		public void Stop()
		{
			_started = false;
			if (_findRequiredServicesthread != null)
				_findRequiredServicesthread.Abort();

			if (_newSchedulethread != null)
				_newSchedulethread.Abort();
		}
		#endregion

		#region Configurations
		//==================================

		/// <summary>
		/// Load and translate the services from app.config
		/// </summary>
		private void LoadServicesFromConfigurationFile()
		{
			//base configuration
			foreach (ServiceElement serviceElement in EdgeServicesConfiguration.Current.Services)
			{
				ServiceConfiguration serviceConfiguration = ServiceConfiguration.FromLegacyConfiguration(serviceElement);
				serviceConfiguration.Lock();
				_serviceBaseConfigurations.Add(serviceConfiguration.Name, serviceConfiguration);
			}

			foreach (AccountElement account in EdgeServicesConfiguration.Current.Accounts)
			{
				// Create matching profile
				Profile profile = new Profile()
				{
					Name = account.ID.ToString(),
					ID = account.ID,
					Settings = new Dictionary<string, object>()
					{
						{"AccountID", account.ID}
					}
				};
				_profiles.Add(account.ID, profile);

				foreach (AccountServiceElement accountService in account.Services)
				{
					ServiceConfiguration serviceConfiguration = ServiceConfiguration.FromLegacyConfiguration(
						accountService,
						_serviceBaseConfigurations[accountService.Uses.Element.Name],
						profile
					);
					serviceConfiguration.Lock();

					_serviceConfigurationsToSchedule.Add(serviceConfiguration);
				}
			}
		}

		public ServiceConfiguration GetServiceBaseConfiguration(string configurationName)
		{
			if (!_serviceBaseConfigurations.ContainsKey(configurationName))
				throw new IndexOutOfRangeException(string.Format("Base configuration with name {0} can not be found!", configurationName));
			return _serviceBaseConfigurations[configurationName];

		}

		//==================================
		#endregion

		#region Scheduling algorithms
		//==================================
		/// <summary>
		/// The main method of creating scheduler 
		/// </summary>
		private void Schedule(bool reschedule = false)
		{
			lock (_serviceConfigurationsToSchedule)
			{
				lock (_scheduledServices)
				{
					// Set need reschedule to false in order to avoid more schedule from other threads
					_needReschedule = false;

					#region Manage history and find services to schedule
					// ------------------------------------

					// Move to history ended/canceled items
					lock (_state.HistoryItems)
					{
						// clear services from history that already ran and their maxdiviation pass -> this is to enable to run them again if their is another schedule
						_state.HistoryItems.RemoveAll(k => k.Value.TimeToRun.Add(k.Value.MaxDeviationAfter) < DateTime.Now);

						foreach (var scheduleService in _scheduledServices.RemoveAll(k => k.Value.Canceled == true || k.Value.LegacyInstance.State == Legacy.ServiceState.Ended))
							_state.HistoryItems.Add(scheduleService.Key.GetHashCode(), HistoryItem.FromSchedulingData(scheduleService.Key, scheduleService.Value, SchedulingResult.Canceled));

						_state.Save();
					}

					// Remove pending uninitialized services so they can be rescheduled
					_scheduledServices.RemoveAll(k => k.Value.LegacyInstance.State == Legacy.ServiceState.Uninitialized && k.Key.RequestedTime.Add(k.Key.Rule.MaxDeviationAfter) > DateTime.Now);

					//Get Services for next time line					
					IEnumerable<SchedulingRequest> servicesForNextTimeLine = GetServicesForTimeLine(reschedule);

					// ------------------------------------
					#endregion

					#region Find Match services
					// ------------------------------------

					//Same services or same services with same profile
					foreach (SchedulingRequest schedulingData in servicesForNextTimeLine)
					{
						//if key exist then this service is runing or ended and should nt be schedule again
						if (!_scheduledServices.ContainsKey(schedulingData))
						{
							//Get all services with same configurationID
							var servicesWithSameConfiguration =
								from s in _scheduledServices
								where
									s.SchedulingRequest.Configuration.Name == schedulingData.Configuration.BaseConfiguration.Name && //should be id but no id yet
									s.LegacyInstance.State != Legacy.ServiceState.Ended &&
									s.Canceled == false //runnig or not started yet
								orderby s.ExpectedStartTime ascending
								select s;

							//Get all services with same profileID
							var servicesWithSameProfile =
								from s in _scheduledServices
								where
									s.Configuration.Profile == schedulingData.Configuration.Profile &&
									s.SchedulingRequest.Configuration.Name == schedulingData.Configuration.BaseConfiguration.Name &&
									s.LegacyInstance.State != Legacy.ServiceState.Ended &&
									s.Canceled == false //not deleted
								orderby s.ExpectedStartTime ascending
								select s;

							//Find the first available time this service with specific service and profile
							ServiceInstance serviceInstance = null;
							TimeSpan avgExecutionTime = GetAverageExecutionTime(schedulingData.Configuration.Name, schedulingData.Configuration.Profile.ID, _percentile);

							DateTime baseStartTime = (schedulingData.RequestedTime < DateTime.Now) ? DateTime.Now : schedulingData.RequestedTime;
							DateTime baseEndTime = baseStartTime.Add(avgExecutionTime);
							DateTime calculatedStartTime = baseStartTime;
							DateTime calculatedEndTime = baseEndTime;

							bool found = false;
							while (!found)
							{
								IOrderedEnumerable<KeyValuePair<SchedulingRequest, ServiceInstance>> whereToLookNext = null;

								int countedPerConfiguration = servicesWithSameConfiguration.Count(s => (calculatedStartTime >= s.ExpectedStartTime && calculatedStartTime <= s.ExpectedEndTime) || (calculatedEndTime >= s.ExpectedStartTime && calculatedEndTime <= s.ExpectedEndTime));
								if (countedPerConfiguration < schedulingData.Configuration.MaxConcurrent)
								{
									int countedPerProfile = servicesWithSameProfile.Count(s => (calculatedStartTime >= s.ExpectedStartTime && calculatedStartTime <= s.ExpectedEndTime) || (calculatedEndTime >= s.ExpectedStartTime && calculatedEndTime <= s.ExpectedEndTime));
									if (countedPerProfile < schedulingData.Configuration.MaxConcurrentPerProfile)
									{
										if (schedulingData.Configuration is ServiceInstanceConfiguration)
										{
											var unplannedConfiguration = (ServiceInstanceConfiguration)schedulingData.Configuration;
											serviceInstance = unplannedConfiguration.Instance;
										}
										else
										{
											serviceInstance = ServiceInstance.FromLegacyInstance(
												Legacy.Service.CreateInstance(schedulingData.Configuration.LegacyConfiguration, int.Parse(schedulingData.Configuration.Profile.Settings["AccountID"].ToString())),
												schedulingData.Configuration
											);
										}

										serviceInstance.SchedulingRequest = schedulingData;
										serviceInstance.SchedulingAccuracy = _percentile;
										serviceInstance.ExpectedStartTime = calculatedStartTime;
										serviceInstance.ExpectedEndTime = calculatedEndTime;
										serviceInstance.LegacyInstance.OutcomeReported += new EventHandler(LegacyInstance_OutcomeReported);

										// Legacy stuff
										TimeSpan maxExecutionTime = TimeSpan.FromMilliseconds(avgExecutionTime.TotalMilliseconds * double.Parse(AppSettings.Get(this, "MaxExecutionTimeProduct")));
										serviceInstance.Configuration.MaxExecutionTime = maxExecutionTime;

										found = true;
									}
									else
									{
										whereToLookNext = servicesWithSameProfile;
									}
								}
								else
								{
									whereToLookNext = servicesWithSameConfiguration;
								}

								if (!found)
								{
									if (whereToLookNext == null)
										throw new Exception("This should not have happened.");

									calculatedStartTime = whereToLookNext.Where(s => s.Value.ExpectedEndTime >= calculatedStartTime).Min(s => s.Value.ExpectedEndTime);
									if (calculatedStartTime < DateTime.Now)
										calculatedStartTime = DateTime.Now;

									//Get end time
									calculatedEndTime = calculatedStartTime.Add(avgExecutionTime);

									////remove unfree time from servicePerConfiguration and servicePerProfile							
									if (calculatedStartTime <= _timeLineTo)
									{
										servicesWithSameConfiguration = from s in servicesWithSameConfiguration
																		where s.ExpectedEndTime > calculatedStartTime
																		orderby s.ExpectedStartTime
																		select s;

										servicesWithSameProfile = from s in servicesWithSameProfile
																  where s.ExpectedEndTime > calculatedStartTime
																  orderby s.ExpectedStartTime
																  select s;
									}
								}
							}


							if (serviceInstance.ActualDeviation <= schedulingData.Rule.MaxDeviationAfter || schedulingData.Rule.MaxDeviationAfter == TimeSpan.Zero)
								_scheduledServices.Add(serviceInstance);
						}
					}
					#endregion

					SchedulingInformationEventArgs args = new SchedulingInformationEventArgs();
					args.ScheduleInformation = new Dictionary<SchedulingRequest, ServiceInstanceInfo>();
					foreach (var scheduleService in _scheduledServices)
					{
						args.ScheduleInformation.Add(scheduleService.SchedulingRequest, scheduleService.GetInfo());

					}
					OnNewScheduleCreated(args);
					NotifyServicesToRun();
				}
			}

		}
		void LegacyInstance_OutcomeReported(object sender, EventArgs e)
		{
			Legacy.ServiceInstance instance = (Edge.Core.Services.ServiceInstance)sender;
			if (_tempCompletedServices == null)
				_tempCompletedServices = new List<string>();
			lock (_tempCompletedServices)
			{
				_tempCompletedServices.Add(instance.InstanceID.ToString());
			}


		}
		/// <summary>
		/// Get this time line services 
		/// </summary>
		/// <param name="useCurrentTimeline">if it's for reschedule then the time line is the same as the last schedule</param>
		/// <returns></returns>
		private IEnumerable<SchedulingRequest> GetServicesForTimeLine(bool useCurrentTimeline)
		{
			// Take next timeline if false
			if (!useCurrentTimeline)
			{
				_timeLineFrom = DateTime.Now;
				_timeLineTo = DateTime.Now.Add(_neededScheduleTimeLine);
			}

			List<SchedulingRequest> potentialSchedulingdata = new List<SchedulingRequest>();
			List<SchedulingRequest> finalSchedulingdata = new List<SchedulingRequest>();
			lock (_serviceConfigurationsToSchedule)
			{
				for (int i = 0; i < _serviceConfigurationsToSchedule.Count; i++)
				{
					ServiceConfiguration service = _serviceConfigurationsToSchedule[i];

					foreach (SchedulingRule schedulingRule in service.SchedulingRules)
					{
						// this should never happen
						if (schedulingRule == null)
							continue;

						foreach (TimeSpan time in schedulingRule.Times)
						{
							DateTime requestedTime;
							if (schedulingRule.Scope != SchedulingScope.Unplanned)
								requestedTime = (_timeLineFrom.Date + time).RemoveSeconds();
							else
								requestedTime = schedulingRule.SpecificDateTime;

							while (requestedTime.Date <= _timeLineTo.Date)
							{
								switch (schedulingRule.Scope)
								{
									case SchedulingScope.Day:
									case SchedulingScope.Week:
										int dayOfWeek = (int)requestedTime.DayOfWeek + 1;
										if (!schedulingRule.Days.Contains(dayOfWeek))
											continue;
										break;
									case SchedulingScope.Month:
										int dayOfMonth = requestedTime.Day;
										if (!schedulingRule.Days.Contains(dayOfMonth))
											continue;
										break;
								}


								if (
									(requestedTime >= _timeLineFrom && requestedTime <= _timeLineTo) ||
									(requestedTime <= _timeLineFrom && (schedulingRule.MaxDeviationAfter == TimeSpan.Zero || requestedTime.Add(schedulingRule.MaxDeviationAfter) >= DateTime.Now))
									)
								{
									SchedulingRequest schedulingdata = new SchedulingRequest()
									{
										Configuration = service,
										Rule = schedulingRule,
										RequestedTime = requestedTime
									};

									// special for unplanned
									if (schedulingRule.Scope == SchedulingScope.Unplanned)
									{
										schedulingdata.Guid = schedulingRule.GuidForUnplanned;
										schedulingdata.Rule.MaxDeviationAfter = TimeSpan.FromHours(8);
									}

									potentialSchedulingdata.Add(schedulingdata);
								}
								requestedTime = requestedTime.AddDays(1);
							}
						}


					}
				}

			}
			// Move potential to final only if its not already scheduled and if its not in the history
			foreach (var schedulingdata in potentialSchedulingdata)
			{
				if (!_scheduledServices.ContainsKey(schedulingdata) && !_state.HistoryItems.ContainsKey(schedulingdata.GetHashCode()))
					finalSchedulingdata.Add(schedulingdata);

			}

			return finalSchedulingdata.OrderBy(schedulingdata => schedulingdata.RequestedTime).ThenByDescending(schedulingdata => schedulingdata.Configuration.Priority);
		}
		/// <summary>
		/// Get the average time of service run by configuration id and wanted percentile
		/// </summary>
		/// <param name="configurationID"></param>
		/// <returns></returns>
		private TimeSpan GetAverageExecutionTime(string configurationName, int AccountID, int Percentile)
		{
			long averageExacutionTime;
			string key = string.Format("ConfigurationName:{0},Account:{1},Percentile:{2}", configurationName, AccountID, Percentile);
			try
			{
				if (_servicePerProfileAvgExecutionTimeCash.ContainsKey(key) && _servicePerProfileAvgExecutionTimeCash[key].TimeSaved.Add(_executionTimeCashTimeOutAfter) < DateTime.Now)
				{
					averageExacutionTime = _servicePerProfileAvgExecutionTimeCash[key].AverageExecutionTime;
				}
				else
				{
					using (SqlConnection SqlConnection = new SqlConnection(AppSettings.GetConnectionString(this, "OLTP")))
					{
						SqlConnection.Open();
						using (SqlCommand sqlCommand = DataManager.CreateCommand("ServiceConfiguration_GetExecutionTime(@ConfigName:NvarChar,@Percentile:Int,@ProfileID:Int)", System.Data.CommandType.StoredProcedure))
						{
							sqlCommand.Connection = SqlConnection;
							sqlCommand.Parameters["@ConfigName"].Value = configurationName;
							sqlCommand.Parameters["@Percentile"].Value = Percentile;
							sqlCommand.Parameters["@ProfileID"].Value = AccountID;

							averageExacutionTime = System.Convert.ToInt32(sqlCommand.ExecuteScalar());
							_servicePerProfileAvgExecutionTimeCash[key] = new ServicePerProfileAvgExecutionTimeCash() { AverageExecutionTime = averageExacutionTime, TimeSaved = DateTime.Now };
						}
					}
				}
			}
			catch
			{
				averageExacutionTime = 180;
			}
			return TimeSpan.FromSeconds(averageExacutionTime);
		}
		/// <summary>
		/// 
		/// </summary>
		/// <param name="serviceConfiguration"></param>
		public void AddServiceToSchedule(ServiceConfiguration serviceConfiguration)
		{
			lock (this)
			{
				_serviceConfigurationsToSchedule.Add(serviceConfiguration);
				lock (_tempCompletedServices)
				{
					_tempCompletedServices.Add(serviceConfiguration.Name);
				}
			}
			_needReschedule = true;
		}
		public void AddChildServiceToSchedule(Legacy.ServiceInstance legacyInstance)
		{
			ServiceConfiguration baseConfiguration;
			if (!_serviceBaseConfigurations.TryGetValue(legacyInstance.Configuration.Name, out baseConfiguration))
				throw new KeyNotFoundException(String.Format("No base configuration exists for the service '{0}'.", legacyInstance.Configuration.Name));

			Profile profile;
			if (!_profiles.TryGetValue(legacyInstance.AccountID, out profile))
				throw new KeyNotFoundException(String.Format("No profile exists with the ID '{0}' (account ID).", legacyInstance.AccountID));

			ServiceInstance childInstance = ServiceInstance.FromLegacyInstance(legacyInstance, baseConfiguration, profile);

			// Treat this as an unplanned instance
			childInstance.Configuration.SchedulingRules.Add(SchedulingRule.CreateUnplanned());

			AddServiceToSchedule(childInstance.Configuration);

		}
		/// <summary>
		/// Delete specific instance of service (service for specific time not all the services)
		/// </summary>
		/// <param name="schedulingRequest"></param>
		public void CancelSchedulingRequest(SchedulingRequest schedulingRequest)
		{
			_scheduledServices[schedulingRequest].Canceled = true;
		}
		private void NotifyServicesToRun()
		{
			//DO some checks
			var instancesShouldRun = new Dictionary<SchedulingRequest, ServiceInstance>();
			List<ServiceInstance> instancesToRun = new List<ServiceInstance>();

			lock (_scheduledServices)
			{
				foreach (var scheduleService in _scheduledServices.OrderBy(s => s.ExpectedStartTime))
				{
					if (scheduleService.ExpectedStartTime.Day == DateTime.Now.Day) //same day
					{

						if (
							scheduleService.ExpectedStartTime <= DateTime.Now &&
							(scheduleService.SchedulingRequest.Rule.MaxDeviationAfter == TimeSpan.Zero || scheduleService.SchedulingRequest.RequestedTime.Add(scheduleService.SchedulingRequest.Rule.MaxDeviationAfter) >= DateTime.Now) &&
							scheduleService.LegacyInstance.State == Legacy.ServiceState.Uninitialized
						)
						{
							instancesShouldRun.Add(scheduleService.SchedulingRequest, scheduleService);
						}
					}
				}
				if (instancesShouldRun.Count > 0)
				{
					var shouldRun = instancesShouldRun.OrderBy(s => s.Value.ExpectedStartTime);
					foreach (var instance in shouldRun)
					{
						int countedServicesWithSameConfiguration = _scheduledServices.Count(s => instance.Key.Configuration.BaseConfiguration.Name == s.SchedulingRequest.Configuration.Name &&
																   s.LegacyInstance.State == Legacy.ServiceState.Running &&
																   s.LegacyInstance.State == Legacy.ServiceState.Initializing &&
																   s.LegacyInstance.State != Legacy.ServiceState.Aborting &&
																   s.Canceled == false);
						//cant run!!!!
						if (countedServicesWithSameConfiguration >= instance.Value.Configuration.MaxConcurrent)
							continue;
						int countedServicesWithSameProfile = _scheduledServices.Count(s => instance.Value.Configuration.Profile.ID == s.SchedulingRequest.Configuration.Profile.ID &&
							instance.Key.Configuration.BaseConfiguration.Name == s.SchedulingRequest.Configuration.Name && //should be id but no id yet
																	s.LegacyInstance.State != Legacy.ServiceState.Uninitialized &&
																	s.LegacyInstance.State != Legacy.ServiceState.Ended &&
																   s.LegacyInstance.State != Legacy.ServiceState.Aborting &&
																	s.Canceled == false);
						//cant run!!!
						if (countedServicesWithSameProfile >= instance.Value.Configuration.MaxConcurrentPerProfile)
							continue;
						OnTimeToRun(new ServicesToRunEventArgs() { ServicesToRun = new ServiceInstance[] { instance.Value } });
					}


				}
			}
		}
		/// <summary>
		/// send event for the services which need to be runing
		/// </summary>
		/// <param name="e"></param>
		private void OnTimeToRun(ServicesToRunEventArgs e)
		{
			foreach (ServiceInstance serviceToRun in e.ServicesToRun)
			{
				Log.Write(this.ToString(), string.Format("Service {0} required to run", serviceToRun.Configuration.Name), LogMessageType.Information);
			}
			ServiceRunRequiredEvent(this, e);
		}
		/// <summary>
		/// set event new schedule created
		/// </summary>
		/// <param name="e"></param>
		private void OnNewScheduleCreated(SchedulingInformationEventArgs e)
		{
			NewScheduleCreatedEvent(this, e);

			// temp
			//NotifyServicesToRun();
		}
		public void CleandEndedUnplaned(Legacy.ServiceInstance instance)
		{
			if (instance.State != Legacy.ServiceState.Ended)
				throw new NotSupportedException("You can't clean unplaned service that not ended");
			List<ServiceConfiguration> toRemove = new List<ServiceConfiguration>();
			lock (_scheduledServices)
			{
				foreach (var scheduleEndedService in _scheduledServices)
				{
					if (scheduleEndedService.LegacyInstance.Guid == instance.Guid && scheduleEndedService.SchedulingRequest.Rule.Scope == SchedulingScope.Unplanned)
					{
						toRemove.Add(scheduleEndedService.Configuration);
					}
				}
				lock (_serviceConfigurationsToSchedule)
				{
					foreach (var item in toRemove)
					{
						if (_serviceConfigurationsToSchedule.Contains(item))
							_serviceConfigurationsToSchedule.Remove(item);


					}
				}

			}
		}
		//==================================

		#endregion
	}

	public class ScheduledServices : ICollection<ServiceInstance>
	{
		Dictionary<SchedulingRequest, ServiceInstance> _instanceBySchedulingRequest = new Dictionary<SchedulingRequest, ServiceInstance>();
		List<ServiceInstance> _instanceCollection = new List<ServiceInstance>();
		Dictionary<Guid, ServiceInstance> _instanceByGuid = new Dictionary<Guid, ServiceInstance>();
		public ServiceInstance this[Guid guid]
		{
			get
			{
				return _instanceByGuid[guid];
			}
		}
		public ServiceInstance this[SchedulingRequest schedulingRequest]
		{
			get
			{
				return _instanceBySchedulingRequest[schedulingRequest];
			}
		}

		
		#region ICollection<ServiceInstance> Members

		public void Add(ServiceInstance serviceInstance)
		{
			_instanceCollection.Add(serviceInstance);
			_instanceByGuid.Add(serviceInstance.LegacyInstance.Guid, serviceInstance);
			if (serviceInstance.SchedulingRequest != null)
				_instanceBySchedulingRequest.Add(serviceInstance.SchedulingRequest, serviceInstance);
		}

		public void Clear()
		{
			_instanceBySchedulingRequest.Clear();
			_instanceCollection.Clear();
			_instanceByGuid.Clear();
		}

		public bool Contains(ServiceInstance serviceInstance)
		{
			 return (_instanceByGuid.ContainsKey(serviceInstance.LegacyInstance.Guid));
		}
		public bool ContainsKey(SchedulingRequest schedulingRequest)
		{
			return (_instanceBySchedulingRequest.ContainsKey(schedulingRequest));

		}
		public bool ContainsKey(Guid guid)
		{
			return (_instanceByGuid.ContainsKey(guid));

		}

		public void CopyTo(ServiceInstance[] array, int arrayIndex)
		{
			throw new NotImplementedException();
		}

		public int Count
		{
			get { return _instanceCollection.Count; }
		}

		public bool IsReadOnly
		{
			get { return this.IsReadOnly; }
		}

		public bool Remove(ServiceInstance serviceInstance)
		{
			bool removed=false;
			if (_instanceCollection.Contains(serviceInstance))
			{
				_instanceCollection.Remove(serviceInstance);
				removed = true;
			}
			if (_instanceBySchedulingRequest.ContainsKey(serviceInstance.SchedulingRequest))
			{
				_instanceBySchedulingRequest.Remove(serviceInstance.SchedulingRequest);
				removed = true;
			}
			if (_instanceByGuid.ContainsKey(serviceInstance.LegacyInstance.Guid))
			{
				_instanceByGuid.Remove(serviceInstance.LegacyInstance.Guid);
				removed = true;
			}

			return removed;
					
		}

		#endregion

		#region IEnumerable<ServiceInstance> Members

		public IEnumerator<ServiceInstance> GetEnumerator()
		{
			return (IEnumerator<ServiceInstance>)_instanceCollection.GetEnumerator();
		}

		#endregion

		#region IEnumerable Members

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return (IEnumerator<ServiceInstance>)_instanceCollection.GetEnumerator();
		}

		#endregion
	}

	#region eventargs classes
	public class ServicesToRunEventArgs : EventArgs
	{
		public ServiceInstance[] ServicesToRun;
	}
	public class WillNotRunEventArgs : EventArgs
	{
		public List<SchedulingRequest> WillNotRun = new List<SchedulingRequest>();
	}
	public class SchedulingInformationEventArgs : EventArgs
	{
		public Dictionary<SchedulingRequest, ServiceInstanceInfo> ScheduleInformation;
	}
	#endregion

	#region extensions
	public static class DictionaryExtensions
	{
		public static IEnumerable<KeyValuePair<TKey, TValue>> RemoveAll<TKey, TValue>(this Dictionary<TKey, TValue> dict,
									 Func<KeyValuePair<TKey, TValue>, bool> condition)
		{
			foreach (var cur in dict.Where(condition).ToList())
			{
				dict.Remove(cur.Key);
				yield return cur;
			}
		}
	}
	
	public static class DateTimeExtenstions
	{
		public static DateTime RemoveSeconds(this DateTime time)
		{
			return new DateTime(time.Year, time.Month, time.Day, time.Hour, time.Minute, 0, 0);
		}
	}
	#endregion

}
