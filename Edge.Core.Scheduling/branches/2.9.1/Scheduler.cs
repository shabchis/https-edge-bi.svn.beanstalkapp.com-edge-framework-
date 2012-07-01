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

		private Dictionary<int, Profile> _profiles = new Dictionary<int, Profile>();
		private Dictionary<string, ServiceConfiguration> _serviceBaseConfigurations = new Dictionary<string, ServiceConfiguration>();

		private Dictionary<int, Legacy.ServiceInstance> _legacyInstanceBySchedulingID = new Dictionary<int, Legacy.ServiceInstance>();

		private List<ServiceConfiguration> _serviceConfigurationsToSchedule = new List<ServiceConfiguration>(); //all services from configuration file load to this var		
		private Dictionary<SchedulingData, ServiceInstance> _scheduledServices = new Dictionary<SchedulingData, ServiceInstance>();
		private Dictionary<int, ServiceConfiguration> _servicesPerConfigurationID = new Dictionary<int, ServiceConfiguration>();
		private Dictionary<int, ServiceConfiguration> _servicesPerProfileID = new Dictionary<int, ServiceConfiguration>();
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
		public event EventHandler ServiceRunRequiredEvent;
		//public event EventHandler ServiceNotScheduledEvent;
		public event EventHandler NewScheduleCreatedEvent;
		private volatile bool _needReschedule = false;
		private TimeSpan _executionTimeCashTimeOutAfter;
		private object _sync;
		private bool _started = false;

		#endregion

		public ServiceConfiguration GetServiceBaseConfiguration(string configurationName)
		{
			if (!_serviceBaseConfigurations.ContainsKey(configurationName))
				throw new IndexOutOfRangeException(string.Format("Base configuration with name {0} can not be found!", configurationName));
			return _serviceBaseConfigurations[configurationName];

		}

		/// <summary>
		/// Initialize all the services from configuration file or db4o
		/// </summary>
		/// <param name="getServicesFromConfigFile"></param>
		public Scheduler(bool getServicesFromConfigFile)
		{
			if (getServicesFromConfigFile)
				GetServicesFromConfigurationFile();

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
		/// The main method of creating scheduler 
		/// </summary>
		public void Schedule(bool reschedule = false)
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
					_scheduledServices.RemoveAll(k => k.Value.LegacyInstance.State == Legacy.ServiceState.Uninitialized && k.Key.TimeToRun.Add(k.Key.Rule.MaxDeviationAfter) > DateTime.Now);

					//Get Services for next time line					
					IEnumerable<SchedulingData> servicesForNextTimeLine = GetServicesForTimeLine(reschedule);

					// ------------------------------------
					#endregion

					#region Find Match services
					// ------------------------------------

					//Same services or same services with same profile
					foreach (SchedulingData schedulingData in servicesForNextTimeLine)
					{
						//if key exist then this service is runing or ended and should nt be schedule again
						if (!_scheduledServices.ContainsKey(schedulingData))
						{
							//Get all services with same configurationID
							var servicesWithSameConfiguration =
								from s in _scheduledServices
								where
									s.Key.Configuration.Name == schedulingData.Configuration.BaseConfiguration.Name && //should be id but no id yet
									s.Value.LegacyInstance.State != Legacy.ServiceState.Ended &&
									s.Value.Canceled == false //runnig or not started yet
								orderby s.Value.StartTime ascending
								select s;

							//Get all services with same profileID
							var servicesWithSameProfile =
								from s in _scheduledServices
								where
									s.Value.Configuration.SchedulingProfile == schedulingData.Configuration.SchedulingProfile &&
									s.Key.Configuration.Name == schedulingData.Configuration.BaseConfiguration.Name &&
									s.Value.LegacyInstance.State != Legacy.ServiceState.Ended &&
									s.Value.Canceled == false //not deleted
								orderby s.Value.StartTime ascending
								select s;

							//Find the first available time this service with specific service and profile
							ServiceInstance serviceInstance = null;
							TimeSpan avgExecutionTime = GetAverageExecutionTime(schedulingData.Configuration.Name, schedulingData.Configuration.SchedulingProfile.ID, _percentile);

							DateTime baseStartTime = (schedulingData.TimeToRun < DateTime.Now) ? DateTime.Now : schedulingData.TimeToRun;
							DateTime baseEndTime = baseStartTime.Add(avgExecutionTime);
							DateTime calculatedStartTime = baseStartTime;
							DateTime calculatedEndTime = baseEndTime;

							bool found = false;
							while (!found)
							{
								int countedPerConfiguration = servicesWithSameConfiguration.Count(s => (calculatedStartTime >= s.Value.StartTime && calculatedStartTime <= s.Value.EndTime) || (calculatedEndTime >= s.Value.StartTime && calculatedEndTime <= s.Value.EndTime));
								if (countedPerConfiguration < schedulingData.Configuration.MaxConcurrent)
								{
									int countedPerProfile = servicesWithSameProfile.Count(s => (calculatedStartTime >= s.Value.StartTime && calculatedStartTime <= s.Value.EndTime) || (calculatedEndTime >= s.Value.StartTime && calculatedEndTime <= s.Value.EndTime));
									if (countedPerProfile < schedulingData.Configuration.MaxConcurrentPerProfile)
									{
										if (schedulingData.Configuration is UnplannedServiceConfiguration)
										{
											var unplannedConfiguration = (UnplannedServiceConfiguration)schedulingData.Configuration;
											serviceInstance = unplannedConfiguration.UnplannedInstance;
										}
										else
										{
											serviceInstance = ServiceInstance.FromLegacyInstance(
												Legacy.Service.CreateInstance(schedulingData.LegacyConfiguration, schedulingData.Profile.ID),
												schedulingData.Configuration
											);
										}

										serviceInstance.ScheduledID = schedulingData.GetHashCode();
										serviceInstance.StartTime = calculatedStartTime;
										serviceInstance.EndTime = calculatedEndTime;
										serviceInstance.Odds = _percentile;
										serviceInstance.ActualDeviation = calculatedStartTime.Subtract(schedulingData.TimeToRun);
										serviceInstance.Priority = schedulingData.Priority;
										serviceInstance.MaxDeviationAfter = schedulingData.Rule.MaxDeviationAfter;


										//if (_legacyInstanceBySchedulingID.ContainsKey(serviceInstance.ScheduledID))
										//    serviceInstance.LegacyInstance = _legacyInstanceBySchedulingID[serviceInstance.ScheduledID];
										//else
										//{
										//    serviceInstance.LegacyInstance = Legacy.Service.CreateInstance(schedulingData.LegacyConfiguration, serviceInstance.ProfileID);
										//    _legacyInstanceBySchedulingID.Add(serviceInstance.ScheduledID, serviceInstance.LegacyInstance);
										//}

										// Legacy stuff
										TimeSpan maxExecutionTime = TimeSpan.FromMilliseconds(avgExecutionTime.TotalMilliseconds * double.Parse(AppSettings.Get(this, "MaxExecutionTimeProduct")));
										serviceInstance.LegacyInstance.Configuration.MaxExecutionTime = maxExecutionTime;
										serviceInstance.LegacyInstance.TimeScheduled = calculatedStartTime;

										found = true;
									}
									else
									{
										calculatedStartTime = servicesWithSameProfile.Where(s => s.Value.EndTime >= calculatedStartTime).Min(s => s.Value.EndTime);
										if (calculatedStartTime < DateTime.Now)
											calculatedStartTime = DateTime.Now;
										//Get end time
										calculatedEndTime = calculatedStartTime.Add(avgExecutionTime);
										////remove unfree time from servicePerConfiguration and servicePerProfile							
										if (calculatedStartTime <= _timeLineTo)
										{
											servicesWithSameConfiguration = from s in servicesWithSameConfiguration
																			where s.Value.EndTime > calculatedStartTime
																			orderby s.Value.StartTime
																			select s;
											servicesWithSameProfile = from s in servicesWithSameProfile
																	  where s.Value.EndTime > calculatedStartTime
																	  orderby s.Value.StartTime
																	  select s;
										}
									}
								}
								else
								{

									calculatedStartTime = servicesWithSameConfiguration.Where(s => s.Value.EndTime >= calculatedStartTime).Min(s => s.Value.EndTime);
									if (calculatedStartTime < DateTime.Now)
										calculatedStartTime = DateTime.Now;
									//Get end time
									calculatedEndTime = calculatedStartTime.Add(avgExecutionTime);
									////remove unfree time from servicePerConfiguration and servicePerProfile							
									if (calculatedStartTime <= _timeLineTo)
									{
										servicesWithSameConfiguration = from s in servicesWithSameConfiguration
																		where s.Value.EndTime > calculatedStartTime
																		orderby s.Value.StartTime
																		select s;

										servicesWithSameProfile = from s in servicesWithSameProfile
																  where s.Value.EndTime > calculatedStartTime
																  orderby s.Value.StartTime
																  select s;
									}
								}
							}
					#endregion
							#region Add the service to schedule table and notify temporarly unschedlued
							KeyValuePair<SchedulingData, ServiceInstance> serviceInstanceAndSchedulingRule = new KeyValuePair<SchedulingData, ServiceInstance>(schedulingData, serviceInstance);
							if (serviceInstanceAndSchedulingRule.Value.ActualDeviation <= serviceInstanceAndSchedulingRule.Key.Rule.MaxDeviationAfter)// || serviceInstanceAndSchedulingRule.Key.Rule.Scope != SchedulingScope.Unplanned)
								_scheduledServices.Add(serviceInstanceAndSchedulingRule.Key, serviceInstanceAndSchedulingRule.Value);

							#endregion
						}
					}

					OnNewScheduleCreated(new ScheduledInformationEventArgs() { ScheduleInformation = _scheduledServices });
				}
			}

		}
		/// <summary>
		/// Return all the services not started to run or did not finished runing
		/// </summary>
		/// <returns></returns>
		public IEnumerable<KeyValuePair<SchedulingData, Edge.Core.Scheduling.Objects.ServiceInstance>> GetScheduldServicesWithStatusNotEnded()
		{
			//Dictionary<SchedulingData, ServiceInstance> returnObject;
			var returnObject = from s in _scheduledServices
							   where s.Value.LegacyInstance.State != Legacy.ServiceState.Ended
							   select s;
			return returnObject;
		}
		/// <summary>
		/// returns all scheduled services
		/// </summary>
		/// <returns></returns>
		public IEnumerable<KeyValuePair<SchedulingData, ServiceInstance>> GetAlllScheduldServices()
		{
			var returnObject = from s in _scheduledServices
							   select s;
			return returnObject;
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

			UnplannedServiceConfiguration serviceConfiguration = UnplannedServiceConfiguration.FromLegacyConfiguration(
				legacyInstance,
				baseConfiguration,
				profile
			);
			serviceConfiguration.SchedulingRules.Add(SchedulingRule.CreateUnplanned());
			serviceConfiguration.UnplannedInstance = ServiceInstance.FromLegacyInstance(legacyInstance, serviceConfiguration);
			lock (_serviceConfigurationsToSchedule)
			{
				_serviceConfigurationsToSchedule.Add(serviceConfiguration);
			}

			AddServiceToSchedule(serviceConfiguration);

		}
		/// <summary>
		/// Get this time line services 
		/// </summary>
		/// <param name="useCurrentTimeline">if it's for reschedule then the time line is the same as the last schedule</param>
		/// <returns></returns>
		private IEnumerable<SchedulingData> GetServicesForTimeLine(bool useCurrentTimeline)
		{
			// Take next timeline if false
			if (!useCurrentTimeline)
			{
				_timeLineFrom = DateTime.Now;
				_timeLineTo = DateTime.Now.Add(_neededScheduleTimeLine);
			}

			List<SchedulingData> potentialSchedulingdata = new List<SchedulingData>();
			List<SchedulingData> finalSchedulingdata = new List<SchedulingData>();
			lock (_serviceConfigurationsToSchedule)
			{
				for (int i = 0; i < _serviceConfigurationsToSchedule.Count; i++)
				{
					ServiceConfiguration service = _serviceConfigurationsToSchedule[i];
					foreach (SchedulingRule schedulingRule in _serviceConfigurationsToSchedule[i].SchedulingRules)
					{
						// this should never happen
						if (schedulingRule == null)
							continue;

						foreach (TimeSpan time in schedulingRule.Times)
						{
							DateTime timeToRun;
							if (schedulingRule.Scope != SchedulingScope.Unplanned)
								timeToRun = (_timeLineFrom.Date + time).RemoveSeconds();
							else
								timeToRun = schedulingRule.SpecificDateTime;



							while (timeToRun.Date <= _timeLineTo.Date)
							{
								switch (schedulingRule.Scope)
								{
									case SchedulingScope.Day:
									case SchedulingScope.Week:
										int dayOfWeek = (int)timeToRun.DayOfWeek + 1;
										if (!schedulingRule.Days.Contains(dayOfWeek))
											continue;
										break;
									case SchedulingScope.Month:
										int dayOfMonth = timeToRun.Day;
										if (!schedulingRule.Days.Contains(dayOfMonth))
											continue;
										break;
								}


								if (
									(timeToRun >= _timeLineFrom && timeToRun <= _timeLineTo) ||
									(timeToRun <= _timeLineFrom && timeToRun.Add(schedulingRule.MaxDeviationAfter) >= DateTime.Now)
									)
								{
									SchedulingData schedulingdata = new SchedulingData()
									{
										Configuration = service,
										Profile = new Profile() { ID = service.SchedulingProfile.ID },
										Rule = schedulingRule,
										SelectedDay = (int)(DateTime.Now.DayOfWeek) + 1,
										SelectedHour = time,
										Priority = service.Priority,
										///TODO:ActiveServiceElement cast not sure if it's right
										LegacyConfiguration = (ActiveServiceElement)service.LegacyConfiguration,
										TimeToRun = timeToRun
									};

									// special for unplanned
									if (schedulingRule.Scope == SchedulingScope.Unplanned)
									{
										schedulingdata.Guid = schedulingRule.GuidForUnplanned;
										schedulingdata.Rule.MaxDeviationAfter = new TimeSpan(8, 0, 0);
									}

									potentialSchedulingdata.Add(schedulingdata);
								}
								timeToRun = timeToRun.AddDays(1);
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
			//finalSchedulingdata.AddRange(potentialSchedulingdata.TakeWhile(schedulingdata =>
			//                    !_scheduledServices.ContainsKey(schedulingdata) && !_state.HistoryItems.ContainsKey(schedulingdata.GetHashCode())));
			return finalSchedulingdata.OrderBy(s => s.TimeToRun).ThenByDescending(s => s.Priority);
		}

		/// <summary>
		/// Load and translate the services from app.config
		/// </summary>
		private void GetServicesFromConfigurationFile()
		{
			//base configuration
			foreach (ServiceElement serviceElement in EdgeServicesConfiguration.Current.Services)
			{
				ServiceConfiguration serviceConfiguration = ServiceConfiguration.FromLegacyConfiguration(serviceElement);
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

					_serviceConfigurationsToSchedule.Add(serviceConfiguration);
				}
			}
		}

		/*
		private void AddServiceWorkflowSteps(ServiceConfiguration serviceConfiguration)
		{
			foreach(WorkflowStepElement step in serviceConfiguration.BaseConfiguration.LegacyConfiguration.Workflow)
			{
				ServiceConfiguration stepConfiguration = ServiceConfiguration.FromLegacyConfiguration(step,
					_serviceBaseConfigurations[step.BaseConfiguration.Element.Name],
					serviceConfiguration.SchedulingProfile);

				_serviceProfileConfigurations.Add(stepConfiguration);

				AddServiceWorkflowSteps(stepConfiguration);
			}
		}
		*/

		/// <summary>
		/// Delete specific instance of service (service for specific time not all the services)
		/// </summary>
		/// <param name="schedulingData"></param>
		public void DeleteScpecificServiceInstance(SchedulingData schedulingData)
		{
			_scheduledServices[schedulingData].Canceled = true;
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
				while (true)
				{
					Thread.Sleep(_intervalBetweenNewSchedule);
					Schedule(false);
				}
			}
			));

			_findRequiredServicesthread = new Thread(new ThreadStart(delegate()
			{
				while (true)
				{
					Thread.Sleep(_findServicesToRunInterval);//TODO: ADD CONST
					NotifyServicesToRun();
				}
			}));


			_newSchedulethread.IsBackground = true;
			_newSchedulethread.Start();
			_findRequiredServicesthread.IsBackground = true;
			_findRequiredServicesthread.Start();

		}

		private void NotifyServicesToRun()
		{
			//DO some checks
			Dictionary<SchedulingData, ServiceInstance> instancesShouldRun = new Dictionary<SchedulingData, ServiceInstance>();
			List<ServiceInstance> instancesToRun = new List<ServiceInstance>();
			if (_needReschedule)
			{
				_needReschedule = false;
				Schedule(true);
			}
			lock (_scheduledServices)
			{
				foreach (var scheduleService in _scheduledServices.OrderBy(s => s.Value.StartTime))
				{
					if (scheduleService.Value.StartTime.Day == DateTime.Now.Day) //same day
					{

						if (scheduleService.Value.StartTime <= DateTime.Now &&
							scheduleService.Key.TimeToRun.Add(scheduleService.Key.Rule.MaxDeviationAfter) >= DateTime.Now &&
						   scheduleService.Value.LegacyInstance.State == Legacy.ServiceState.Uninitialized)
							instancesShouldRun.Add(scheduleService.Key, scheduleService.Value);
					}
				}
				if (instancesShouldRun.Count > 0)
				{
					var shouldRun = instancesShouldRun.OrderBy(s => s.Value.StartTime);
					foreach (var instance in shouldRun)
					{
						int countedServicesWithSameConfiguration = _scheduledServices.Count(s => instance.Key.Configuration.BaseConfiguration.Name == s.Key.Configuration.Name &&
																   s.Value.LegacyInstance.State == Legacy.ServiceState.Running &&
																   s.Value.LegacyInstance.State == Legacy.ServiceState.Initializing &&
																   s.Value.LegacyInstance.State != Legacy.ServiceState.Aborting &&
																   s.Value.Canceled == false);
						//cant run!!!!
						if (countedServicesWithSameConfiguration >= instance.Value.Configuration.MaxConcurrent)
							continue;
						int countedServicesWithSameProfile = _scheduledServices.Count(s => instance.Value.Configuration.SchedulingProfile.ID == s.Key.Configuration.SchedulingProfile.ID &&
							instance.Key.Configuration.BaseConfiguration.Name == s.Key.Configuration.Name && //should be id but no id yet
																	s.Value.LegacyInstance.State != Legacy.ServiceState.Uninitialized &&
																	s.Value.LegacyInstance.State != Legacy.ServiceState.Ended &&
																   s.Value.LegacyInstance.State != Legacy.ServiceState.Aborting &&
																	s.Value.Canceled == false);
						//cant run!!!
						if (countedServicesWithSameProfile >= instance.Value.Configuration.MaxConcurrentPerProfile)
							continue;
						OnTimeToRun(new TimeToRunEventArgs() { ServicesToRun = new ServiceInstance[] { instance.Value } });
					}


				}
			}
			instancesShouldRun.Clear();
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
		public Legacy.IsAlive IsAlive(Guid guid)
		{
			Legacy.IsAlive alive;
			var instance = _scheduledServices.Where(i => i.Value.LegacyInstance.Guid == guid); //Get from legacyInstance
			if (instance.Count() > 0)
				alive = instance.ToList()[0].Value.LegacyInstance.IsAlive();
			else
			{
				instance = _scheduledServices.Where(i => i.Key.Guid == guid); //Get from scheduling guid
				if (instance.Count() > 0)
					alive = instance.ToList()[0].Value.LegacyInstance.IsAlive();
				else //finished so take from history
				{
					alive = new Legacy.IsAlive();
					var item = _state.HistoryItems.Where(h => h.Value.Guid == guid);
					if (item.Count() > 0)
					{
						HistoryItem historyItem = item.ToList()[0].Value;
						alive.Guid = historyItem.Guid;
						alive.Outcome = historyItem.ServiceOutcome;
					}
					else
						alive.State = string.Format("Service with Guid {0} not found", guid);
				}
			}
			return alive;
		}
		/// <summary>
		/// send event for the services which need to be runing
		/// </summary>
		/// <param name="e"></param>
		public void OnTimeToRun(TimeToRunEventArgs e)
		{
			foreach (ServiceInstance serviceToRun in e.ServicesToRun)
			{
				Log.Write(this.ToString(), string.Format("Service {0} required to run", serviceToRun.ServiceName), LogMessageType.Information);
			}
			ServiceRunRequiredEvent(this, e);
		}
		/// <summary>
		/// set event new schedule created
		/// </summary>
		/// <param name="e"></param>
		public void OnNewScheduleCreated(ScheduledInformationEventArgs e)
		{
			NewScheduleCreatedEvent(this, e);

			// temp
			NotifyServicesToRun();
		}
		/// <summary>
		/// abort runing service
		/// </summary>
		/// <param name="schedulingData"></param>
		public void AbortRuningService(SchedulingData schedulingData)
		{
			_scheduledServices[schedulingData].LegacyInstance.Abort();
		}



		public void RestUnEnded()
		{
			using (SqlConnection SqlConnection = new SqlConnection(AppSettings.GetConnectionString(this, "System")))
			{
				SqlConnection.Open();
				using (SqlCommand sqlCommand = new SqlCommand("ResetUnendedServices", SqlConnection))
				{
					sqlCommand.CommandType = System.Data.CommandType.StoredProcedure;
					sqlCommand.ExecuteNonQuery();


				}
			}
		}

		public List<AccountServiceInformation> GetServicesConfigurations()
		{
			List<AccountServiceInformation> accounsServiceInformation = new List<AccountServiceInformation>();
			foreach (AccountElement account in EdgeServicesConfiguration.Current.Accounts)
			{
				AccountServiceInformation accounServiceInformation;
				accounServiceInformation = new AccountServiceInformation() { AccountName = account.Name, ID = account.ID };
				accounServiceInformation.Services = new List<string>();
				foreach (AccountServiceElement service in account.Services)
					accounServiceInformation.Services.Add(service.Name);
				accounsServiceInformation.Add(accounServiceInformation);
			}
			return accounsServiceInformation;

		}

		public Legacy.ServiceInstance GetInstance(Guid guid)
		{
			var instance = _scheduledServices.Where(i => i.Value.LegacyInstance.Guid == guid); //Get from legacyInstance
			if (instance.Count() > 0)
				return instance.ToList()[0].Value.LegacyInstance;
			else
				throw new Exception(string.Format("Instance with guid {0} not found!", guid));
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
					if (scheduleEndedService.Value.LegacyInstance.Guid == instance.Guid && scheduleEndedService.Key.Rule.Scope == SchedulingScope.Unplanned)
					{
						toRemove.Add(scheduleEndedService.Value.Configuration);
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
	}
	#region eventargs classes
	public class TimeToRunEventArgs : EventArgs
	{
		public ServiceInstance[] ServicesToRun;
	}
	public class ScheduledInformationEventArgs : EventArgs
	{
		public Dictionary<SchedulingData, ServiceInstance> ScheduleInformation;
	}
	public class WillNotRunEventArgs : EventArgs
	{
		public List<SchedulingData> WillNotRun = new List<SchedulingData>();
	}
	public class SchedulingInformationEventArgs : EventArgs
	{
		public Dictionary<SchedulingData, ServiceInstanceInfo> ScheduleInformation;
	}
	#endregion
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
}
