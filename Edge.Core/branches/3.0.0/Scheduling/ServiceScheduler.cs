using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Data.SqlClient;

namespace Edge.Core.Services2.Scheduling
{
	public class ServiceScheduler
	{
		#region Fields
		//======================

		private List<ServiceConfiguration> _servicesWarehouse = new List<ServiceConfiguration>(); //all services from configuration file load to this var
		private Dictionary<SchedulingInfo, ServiceInstance> _scheduledServices = new Dictionary<SchedulingInfo, ServiceInstance>();
		private Dictionary<int, ServiceConfiguration> _servicesPerConfigurationID = new Dictionary<int, ServiceConfiguration>();
		private Dictionary<int, ServiceConfiguration> _servicesPerProfileID = new Dictionary<int, ServiceConfiguration>();
		private Dictionary<SchedulingInfo, ServiceInstance> _unscheduleServices = new Dictionary<SchedulingInfo, ServiceInstance>();
		//contains average execution time per services per account
		private Dictionary<string, ServicePerProfileAvgExecutionTimeCache> _servicePerProfileAvgExecutionTimeCash = new Dictionary<string, ServicePerProfileAvgExecutionTimeCache>();
		DateTime _timeLineFrom;
		DateTime _timeLineTo;
		private TimeSpan _neededScheduleTimeLine; //scheduling for the next xxx min....
		private int _percentile = 80; //execution time of specifc service on sprcific Percentile
		private TimeSpan _intervalBetweenNewSchedule;
		private TimeSpan _findServicesToRunInterval;
		private Thread _findRequiredServicesthread;
		private TimeSpan _timeToDeleteServiceFromTimeLine;
		//how much time until we take the average execution from the database and not from the cache
		private TimeSpan _executionTimeCacheTimeOutAfter;
		private Thread _newSchedulethread;
		//public event EventHandler ServiceNotScheduledEvent;
		private volatile bool _needReschedule = false;

		public ServiceEnvironment Environment { get; private set; }

		//======================
		#endregion

		#region Events
		//======================

		public event EventHandler ServiceRunRequired;
		public event EventHandler NewScheduleCreated;

		//======================
		#endregion


		#region Constructors
		//======================

		/// <summary>
		/// Initialize all the services from configuration file or db4o
		/// </summary>
		/// <param name="getServicesFromConfigFile"></param>
		public ServiceScheduler(ServiceEnvironment environment, bool getServicesFromConfigFile)
		{
			if (environment == null)
				throw new ArgumentNullException("environment");

			this.Environment = environment;

			if (getServicesFromConfigFile)
				GetServicesFromConfigurationFile();


			_percentile = 80; //int.Parse(AppSettings.Get(this, "Percentile"));
			_neededScheduleTimeLine = TimeSpan.FromHours(2); //TimeSpan.Parse(AppSettings.Get(this, "NeededScheduleTimeLine"));
			_intervalBetweenNewSchedule = TimeSpan.FromMinutes(10); //TimeSpan.Parse(AppSettings.Get(this, "IntervalBetweenNewSchedule"));
			_findServicesToRunInterval = TimeSpan.FromMinutes(1); //TimeSpan.Parse(AppSettings.Get(this, "FindServicesToRunInterval"));
			_timeToDeleteServiceFromTimeLine = TimeSpan.FromDays(1); //TimeSpan.Parse(AppSettings.Get(this, "DeleteEndedServiceInterval"));
			_executionTimeCacheTimeOutAfter = TimeSpan.FromMinutes(15); //TimeSpan.Parse(AppSettings.Get(this, "DeleteEndedServiceInterval"));
		}


		//======================
		#endregion


		/// <summary>
		/// The main method of creating schedule 
		/// </summary>
		public void Schedule(bool reschedule)
		{
			lock (_sync)
			{
				//set need reschedule to false in order to avoid more schedule from other threads
				_needReschedule = false;

				//Get next time line
				IOrderedEnumerable<ServiceInstance> servicesForNextTimeLine = GetServicesForNextTimeLine(
					reschedule && _timeLineFrom != DateTime.MinValue, // reschedule
					true // prioritze
					);

				#region Cleanup
				//====================================

				//..............................

				// check if the scheduling info is not already in use - if it is, remove it in order to reschedule in the current operation
				foreach (ServiceInstance instance in servicesForNextTimeLine)
				{
					// TODO: check if we can reuse the instance instead of discarding it
					if (_scheduledServices.ContainsKey(instance.SchedulingInfo) && instance.State == ServiceState.Uninitialized)
						_scheduledServices.Remove(instance.SchedulingInfo);
				}

				//services that did not run because their base time + maxdiviation<datetime.now 
				//should have been run but from some reason did not run
				foreach (KeyValuePair<SchedulingInfo, ServiceInstance> scheduldService in _scheduledServices)
				{
					if
					(
						scheduldService.Value.State == ServiceState.Uninitialized &&
						scheduldService.Key.RequestedTimeStart + scheduldService.Key.Rule.MaxDeviationAfter < DateTime.Now
					)
					{
						// Abort the service and mark it as could not be scheduled
						scheduldService.Value.Abort(ServiceOutcome.CouldNotBeScheduled);
					}
				}

				//..............................
				// Services that has ended and we want to see them for configured time 

				List<ServiceInstance> endedAndTimeToClear = new List<ServiceInstance>();
				foreach (var scheduledService in _scheduledServices)
				{
					if (scheduledService.Value.State == ServiceState.Ended)//if service ended 
						endedAndTimeToClear.Add(scheduledService.Value);
				}
				lock (_servicesWarehouse)
				{
					foreach (ServiceInstance toClear in endedAndTimeToClear)
					{
						_scheduledServices.Remove(toClear.SchedulingInfo); //clear from already schedule table
						toBeScheduledByTimeAndPriority.Remove(toClear); //clear from to be scheduled on the curent new schedule
						if (toClear.SchedulingInfo.Rule.Scope == SchedulingScope.Unplanned)
							_servicesWarehouse.Remove(toClear.Configuration); //clear from services in services wherhouse(all services from configuration and unplaned)

						// Just in case the connection is still up (should never happen)
						((IDisposable)toClear).Dispose();
					}
				}


				//====================================
				#endregion

				// Will include services that could not be fitted into the current schedule, but might be used later
				List<ServiceInstance> temporarilyCouldNotBeScheduled = new List<ServiceInstance>();

				foreach (ServiceInstance toBeScheduledInstance in servicesForNextTimeLine)
				{
					//if key exist then this service is runing or ednededule again
					if (_scheduledServices.ContainsKey(toBeScheduledInstance.SchedulingInfo))
						continue;


					// Get all services with same configuration
					var servicesWithSameConfiguration = from scheduled in _scheduledServices
														where
															scheduled.Value.Configuration.ByLevel(ServiceConfigurationLevel.Global) == toBeScheduledInstance.Configuration.ByLevel(ServiceConfigurationLevel.Global) && //should be id but no id yet
															scheduled.Value.State != ServiceState.Ended
														orderby
															scheduled.Value.SchedulingInfo.PlannedTimeStart ascending
														select
															scheduled;

					//Get all services with same profile
					var servicesWithSameProfile = from scheduled in _scheduledServices
												  where
												  scheduled.Value.Configuration.ByLevel(ServiceConfigurationLevel.Profile) == toBeScheduledInstance.Configuration.ByLevel(ServiceConfigurationLevel.Profile) &&
												  scheduled.Value.State != ServiceState.Ended
												  orderby
												  scheduled.Value.SchedulingInfo.PlannedTimeStart ascending
												  select
												  scheduled;




					ServiceInstance serviceInstance = FindFirstFreeTime(servicesWithSameConfiguration, servicesWithSameProfile, instance);
					KeyValuePair<SchedulingInfo, ServiceInstance> serviceInstanceAndRuleHash = new KeyValuePair<SchedulingInfo, ServiceInstance>(instance, serviceInstance);


					if (serviceInstanceAndRuleHash.Key.ActualDeviation > serviceInstanceAndRuleHash.Key.Rule.MaxDeviationAfter && serviceInstanceAndRuleHash.Key.Rule.Scope != SchedulingScope.Unplanned)
					{
						// check if the waiting time is bigger then max waiting time.
						temporarilyCouldNotBeScheduled.Add(serviceInstanceAndRuleHash.Key, serviceInstanceAndRuleHash.Value);
						//Log.Write(this.ToString(), string.Format("Service {0} not schedule since it's scheduling exceed max MaxDeviation", serviceInstanceAndRuleHash.Value.ServiceName), LogMessageType.Warning);

					}
					else
					{
						_scheduledServices.Add(serviceInstanceAndRuleHash.Key, serviceInstanceAndRuleHash.Value);
					}



				}
			}

			OnNewScheduleCreated(new ScheduledInformationEventArgs() { NotScheduledInformation = temporarilyCouldNotBeScheduled, ScheduleInformation = _scheduledServices });
		}



		/// <summary>
		/// Clear Services for reschedule them-it will only clean the services that is in the next time line.
		/// </summary>
		/// <param name="toBeScheduledByTimeAndPriority"></param>
		private void ClearServicesforReschedule(List<ServiceInstance> toBeScheduledByTimeAndPriority)
		{



		}






		/// <summary>
		/// add unplanned service to schedule
		/// </summary>
		/// <param name="serviceConfiguration"></param>
		public void AddNewServiceToSchedule(ServiceConfiguration serviceConfiguration)
		{
			lock (_servicesWarehouse)
			{

				_servicesWarehouse.Add(serviceConfiguration);
			}
			_needReschedule = true;
		}

		/// <summary>
		/// Get this time line services 
		/// </summary>
		/// <param name="reschedule">if it's for reschedule then the time line is the same as the last schedule</param>
		/// <returns></returns>
		private IOrderedEnumerable<ServiceInstance> GetServicesForNextTimeLine(bool reschedule, bool prioritize)
		{
			if (!reschedule)
			{
				_timeLineFrom = DateTime.Now;
				_timeLineTo = DateTime.Now.Add(_neededScheduleTimeLine);
			}

			var instances = new List<ServiceInstance>();

			lock (_servicesWarehouse)
			{
				for (int i = 0; i < _servicesWarehouse.Count; i++)
				{
					foreach (SchedulingRule schedulingRule in _servicesWarehouse[i].SchedulingRules)
					{
						ServiceConfiguration serviceConfig = _servicesWarehouse[i];
						if (schedulingRule != null)
						{
							foreach (TimeSpan hour in schedulingRule.Hours)
							{
								ServiceInstance newInstance = null;

								switch (schedulingRule.Scope)
								{
									case SchedulingScope.Day:
										{
											DateTime timeToRun = _timeLineFrom.Date;

											timeToRun = timeToRun + hour;
											timeToRun = new DateTime(timeToRun.Year, timeToRun.Month, timeToRun.Day, timeToRun.Hour, timeToRun.Minute, 0, 0); //remove seconds/miliseconds/ticks
											while (timeToRun.Date <= _timeLineTo.Date)
											{
												if (timeToRun >= _timeLineFrom && timeToRun <= _timeLineTo || timeToRun <= _timeLineFrom && timeToRun.Add(schedulingRule.MaxDeviationAfter) >= DateTime.Now)
												{
													newInstance = this.Environment.CreateServiceInstance(serviceConfig);
													newInstance.SchedulingInfo = new SchedulingInfo(newInstance)
													{
														Rule = schedulingRule,
														SelectedDay = (int)(DateTime.Now.DayOfWeek) + 1,
														SelectedHour = hour,
														RequestedTimeStart = timeToRun
													};

												}
												timeToRun = timeToRun.AddDays(1);

											}

											break;
										}
									case SchedulingScope.Week:
										{
											//will not work if schedulelength is more then two days(and it shouldnot)
											DateTime timeToRunFrom = _timeLineFrom.Date + hour;
											timeToRunFrom = new DateTime(timeToRunFrom.Year, timeToRunFrom.Month, timeToRunFrom.Day, timeToRunFrom.Hour, timeToRunFrom.Minute, 0, 0); //remove seconds/miliseconds/ticks
											DateTime timeToRunTo = _timeLineTo.Date + hour;
											timeToRunTo = new DateTime(timeToRunTo.Year, timeToRunTo.Month, timeToRunTo.Day, timeToRunTo.Hour, timeToRunTo.Minute, 0, 0); //remove seconds/miliseconds/ticks
											foreach (int day in schedulingRule.Days)
											{

												if (day == (int)timeToRunFrom.DayOfWeek + 1)
												{
													if (timeToRunFrom >= _timeLineFrom && timeToRunFrom <= _timeLineTo || timeToRunFrom <= _timeLineFrom && timeToRunFrom.Add(schedulingRule.MaxDeviationAfter) >= DateTime.Now)
													{
														newInstance = this.Environment.CreateServiceInstance(serviceConfig);
														newInstance.SchedulingInfo = new SchedulingInfo(newInstance)
														{
															Rule = schedulingRule,
															SelectedDay = (int)(DateTime.Now.DayOfWeek) + 1,
															SelectedHour = hour,
															RequestedTimeStart = timeToRunFrom
														};

														// TODO: do this at end of loop
														//	foundedSchedulingInfo.Add(schedulingInfo);

													}

												}
												if (day == (int)timeToRunTo.DayOfWeek + 1)
												{
													if (timeToRunTo >= _timeLineFrom && timeToRunTo <= _timeLineTo || timeToRunTo <= _timeLineFrom && timeToRunTo.Add(schedulingRule.MaxDeviationAfter) >= DateTime.Now)
													{
														newInstance = this.Environment.CreateServiceInstance(serviceConfig);
														newInstance.SchedulingInfo = new SchedulingInfo(newInstance)
														{
															Rule = schedulingRule,
															SelectedDay = (int)(DateTime.Now.DayOfWeek) + 1,
															SelectedHour = hour,
															RequestedTimeStart = timeToRunTo
														};

													}
												}

											}
											break;
										}
									case SchedulingScope.Month://TODO: 31,30,29 of month can be problematicly
										{
											DateTime timeToRunFrom = _timeLineFrom.Date + hour;
											timeToRunFrom = new DateTime(timeToRunFrom.Year, timeToRunFrom.Month, timeToRunFrom.Day, timeToRunFrom.Hour, timeToRunFrom.Minute, 0, 0); //remove seconds/miliseconds/ticks
											DateTime timeToRunTo = _timeLineTo.Date + hour;
											timeToRunTo = new DateTime(timeToRunTo.Year, timeToRunTo.Month, timeToRunTo.Day, timeToRunTo.Hour, timeToRunTo.Minute, 0, 0); //remove seconds/miliseconds/ticks
											foreach (int day in schedulingRule.Days)
											{

												if (day == timeToRunFrom.Day)
												{
													if (timeToRunFrom >= _timeLineFrom && timeToRunFrom <= _timeLineTo || timeToRunFrom <= _timeLineFrom && timeToRunFrom.Add(schedulingRule.MaxDeviationAfter) >= DateTime.Now)
													{
														newInstance = this.Environment.CreateServiceInstance(serviceConfig);
														newInstance.SchedulingInfo = new SchedulingInfo(newInstance)
														{
															Rule = schedulingRule,
															SelectedDay = (int)(DateTime.Now.DayOfWeek) + 1,
															SelectedHour = hour,
															RequestedTimeStart = timeToRunFrom
														};

													}
												}
												if (day == (int)timeToRunTo.DayOfWeek)
												{
													if (timeToRunTo >= _timeLineFrom && timeToRunTo <= _timeLineTo || timeToRunTo <= _timeLineFrom && timeToRunTo.Add(schedulingRule.MaxDeviationAfter) >= DateTime.Now)
													{
														newInstance = this.Environment.CreateServiceInstance(serviceConfig);
														newInstance.SchedulingInfo = new SchedulingInfo(newInstance)
														{
															Rule = schedulingRule,
															SelectedDay = (int)(DateTime.Now.DayOfWeek) + 1,
															SelectedHour = hour,
															RequestedTimeStart = timeToRunTo
														};

													}
												}
											}
											break;
										}
									case SchedulingScope.Unplanned:
										{

											DateTime timeToRun = schedulingRule.SpecificDateTime;
											if (timeToRun >= _timeLineFrom && timeToRun <= _timeLineTo || timeToRun <= _timeLineFrom && timeToRun.Add(schedulingRule.MaxDeviationAfter) > DateTime.Now)
											{
												newInstance = this.Environment.CreateServiceInstance(serviceConfig);
												newInstance.SchedulingInfo = new SchedulingInfo(newInstance)
												{
													Rule = schedulingRule,
													SelectedDay = (int)(DateTime.Now.DayOfWeek) + 1,
													SelectedHour = hour,
													RequestedTimeStart = timeToRun
												};

											}
											break;
										}
								}
								if (newInstance != null)
									instances.Add(newInstance);
							}

						}
					}
				}

			}


			// TODO: improve sorting to include priority and AvergeExecutionTime
			var sorted = instances.OrderBy(s => s.SchedulingInfo.RequestedTimeStart);

			if (prioritize)
			{
				//TODO: .ThenByDescending(s => s.Configuration.Priority);
			}

			return sorted;
		}



		/// <summary>
		/// Load and translate the services from app.config
		/// </summary>
		[Obsolete("Need to convert to new configuration system")]
		private void GetServicesFromConfigurationFile()
		{
			/*
			Dictionary<string, ServiceConfiguration> baseConfigurations = new Dictionary<string, ServiceConfiguration>();
			Dictionary<string, ServiceConfiguration> configurations = new Dictionary<string, ServiceConfiguration>();
			//base configuration
			foreach (ServiceElement serviceElement in ServicesConfiguration.Services)
			{
				ServiceConfiguration serviceConfiguration = new ServiceConfiguration();
				serviceConfiguration.Name = serviceElement.Name;

				serviceConfiguration.MaxConcurrent = serviceElement.MaxInstances;
				serviceConfiguration.MaxCuncurrentPerProfile = serviceElement.MaxInstancesPerAccount;
				//serviceConfiguration.ID = GetServceConfigruationIDByName(serviceConfiguration.Name);
				baseConfigurations.Add(serviceConfiguration.Name, serviceConfiguration);
			}
			//profiles=account and specific aconfiguration
			foreach (AccountElement account in ServicesConfiguration.Accounts)
			{
				foreach (AccountServiceElement accountService in account.Services)
				{
					ServiceElement serviceUse = accountService.Uses.Element;
					//active element is the calculated configuration 
					ActiveServiceElement activeServiceElement = new ActiveServiceElement(accountService);
					ServiceConfiguration serviceConfiguration = new ServiceConfiguration();
					serviceConfiguration.Name = serviceUse.Name;
					if (activeServiceElement.Options.ContainsKey("ServicePriority"))
						serviceConfiguration.priority = int.Parse(activeServiceElement.Options["ServicePriority"]);

					serviceConfiguration.MaxConcurrent = (activeServiceElement.MaxInstances == 0) ? 9999 : activeServiceElement.MaxInstances;
					serviceConfiguration.MaxCuncurrentPerProfile = (activeServiceElement.MaxInstancesPerAccount == 0) ? 9999 : activeServiceElement.MaxInstancesPerAccount;
					serviceConfiguration.LegacyConfiguration = activeServiceElement;
					//scheduling rules 
					foreach (SchedulingRuleElement schedulingRuleElement in activeServiceElement.SchedulingRules)
					{

						SchedulingRule rule = new SchedulingRule();
						switch (schedulingRuleElement.CalendarUnit)
						{


							case CalendarUnit.Day:
								rule.Scope = SchedulingScope.Day;
								break;
							case CalendarUnit.Month:
								rule.Scope = SchedulingScope.Month;
								break;
							case CalendarUnit.Week:
								rule.Scope = SchedulingScope.Week;
								break;
							case CalendarUnit.AlwaysOn:
							case CalendarUnit.ReRun:
								continue; //not supported right now!

						}

						//subunits= weekday,monthdays
						rule.Days = schedulingRuleElement.SubUnits.ToList();
						rule.Hours = schedulingRuleElement.ExactTimes.ToList();
						rule.MaxDeviationAfter = schedulingRuleElement.MaxDeviation;
						if (serviceConfiguration.SchedulingRules == null)
							serviceConfiguration.SchedulingRules = new List<SchedulingRule>();
						serviceConfiguration.SchedulingRules.Add(rule);
					}
					serviceConfiguration.BaseConfiguration = baseConfigurations[serviceUse.Name];
					//profile settings
					Profile profile = new Profile();
					profile.Name = account.ID.ToString();
					profile.ID = account.ID;
					profile.Settings = new Dictionary<string, object>();
					profile.Settings.Add("AccountID", account.ID);
					serviceConfiguration.SchedulingProfile = profile;
					_servicesWarehouse.Add(serviceConfiguration);

				}
			}
			*/
		}








		/// <summary>
		/// The algoritm of finding the the right time for service
		/// </summary>
		/// <param name="servicesWithSameConfiguration"></param>
		/// <param name="servicesWithSameProfile"></param>
		/// <param name="schedulingInfo"></param>
		/// <returns></returns>
		private ServiceInstance FindFirstFreeTime(IOrderedEnumerable<KeyValuePair<SchedulingInfo, ServiceInstance>> servicesWithSameConfiguration, IOrderedEnumerable<KeyValuePair<SchedulingInfo, ServiceInstance>> servicesWithSameProfile, ServiceInstance instance)
		{
			SchedulingInfo schedulingInfo = instance.SchedulingInfo;

			ServiceInstance serviceInstacnce = null;
			TimeSpan executionTimeInSeconds = instance.Configuration.GetStatistics(_percentile).AverageExecutionTime;
			//TimeSpan executionTimeInSeconds = GetAverageExecutionTime(schedulingInfo.Configuration.ServiceName, schedulingInfo.Configuration.Profile.ID, _percentile);

			DateTime baseStartTime = (schedulingInfo.RequestedTimeStart < DateTime.Now) ? DateTime.Now : schedulingInfo.RequestedTimeStart;
			DateTime baseEndTime = baseStartTime.Add(executionTimeInSeconds);
			DateTime calculatedStartTime = baseStartTime;
			DateTime calculatedEndTime = baseEndTime;
			bool found = false;


			while (!found)
			{
				int countedPerConfiguration = servicesWithSameConfiguration.Count(scheduled => (calculatedStartTime >= scheduled.Value.StartTime && calculatedStartTime <= scheduled.Value.EndTime) || (calculatedEndTime >= scheduled.Value.StartTime && calculatedEndTime <= scheduled.Value.EndTime));
				if (countedPerConfiguration < schedulingInfo.Configuration.Limits.MaxConcurrentGlobal)
				{
					int countedPerProfile = servicesWithSameProfile.Count(s => (calculatedStartTime >= s.Value.StartTime && calculatedStartTime <= s.Value.EndTime) || (calculatedEndTime >= s.Value.StartTime && calculatedEndTime <= s.Value.EndTime));
					if (countedPerProfile < schedulingInfo.Configuration.Limits.MaxConcurrentPerProfile)
					{
						serviceInstacnce = this.Environment.CreateServiceInstance();
						serviceInstacnce.StartTime = calculatedStartTime;
						serviceInstacnce.EndTime = calculatedEndTime;
						serviceInstacnce.Odds = _percentile;
						serviceInstacnce.ActualDeviation = calculatedStartTime.Subtract(schedulingInfo.RequestedTimeStart);
						serviceInstacnce.Priority = schedulingInfo.Priority;
						serviceInstacnce.BaseConfigurationID = schedulingInfo.Configuration.BaseConfiguration.ID;
						serviceInstacnce.ID = schedulingInfo.Configuration.ID;
						serviceInstacnce.MaxConcurrentPerConfiguration = schedulingInfo.Configuration.MaxConcurrent;
						serviceInstacnce.MaxCuncurrentPerProfile = schedulingInfo.Configuration.MaxCuncurrentPerProfile;
						serviceInstacnce.MaxDeviationAfter = schedulingInfo.Rule.MaxDeviationAfter;
						serviceInstacnce.ActualDeviation = calculatedStartTime.Subtract(schedulingInfo.RequestedTimeStart);
						serviceInstacnce.MaxDeviationBefore = schedulingInfo.Rule.MaxDeviationBefore;
						serviceInstacnce.ProfileID = schedulingInfo.Configuration.SchedulingProfile.ID;
						serviceInstacnce.LegacyInstance = Legacy.Service.CreateInstance(schedulingInfo.LegacyConfiguration, serviceInstacnce.ProfileID);
						serviceInstacnce.LegacyInstance.StateChanged += new EventHandler(LegacyInstance_StateChanged);
						serviceInstacnce.LegacyInstance.TimeScheduled = calculatedStartTime;
						serviceInstacnce.ServiceName = schedulingInfo.Configuration.Name;
						found = true;
					}
					else
					{
						//get the next first place of ending service(next start time
						GetNewStartEndTime(servicesWithSameProfile, ref calculatedStartTime, ref calculatedEndTime, executionTimeInSeconds);
						////remove unfree time from servicePerConfiguration and servicePerProfile
						RemoveBusyTime(ref servicesWithSameConfiguration, ref servicesWithSameProfile, calculatedStartTime);
					}
				}
				else
				{
					GetNewStartEndTime(servicesWithSameConfiguration, ref calculatedStartTime, ref calculatedEndTime, executionTimeInSeconds);
					////remove unfree time from servicePerConfiguration and servicePerProfile
					RemoveBusyTime(ref servicesWithSameConfiguration, ref servicesWithSameProfile, calculatedStartTime);
				}
			}
			return serviceInstacnce;
		}

		/// <summary>
		/// event handler for change of the state of servics
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		void LegacyInstance_StateChanged(object sender, Legacy.ServiceStateChangedEventArgs e)
		{
			if (e.StateAfter == ServiceState.Ended)
				_needReschedule = true;
		}

		/// <summary>
		/// Get the average time of service run by configuration id and wanted percentile
		/// </summary>
		/// <param name="configurationID"></param>
		/// <returns></returns>
		private TimeSpan GetAverageExecutionTime(string configurationName, int accountID, int percentile)
		{
			long averageExacutionTime;
			string key = string.Format("ConfigurationName:{0},Account:{1},Percentile:{2}", configurationName, accountID, percentile);
			try
			{
				if (_servicePerProfileAvgExecutionTimeCash.ContainsKey(key) && _servicePerProfileAvgExecutionTimeCash[key].TimeSaved.Add(_executionTimeCacheTimeOutAfter) < DateTime.Now)
				{
					averageExacutionTime = _servicePerProfileAvgExecutionTimeCash[key].AverageExecutionTime;
				}
				else
				{
					string connectionString = AppSetting.GetConnectionString("some connection string");
					using (SqlConnection sqlConnection = new SqlConnection(connectionString))
					{
						using (SqlCommand sqlCommand = new SqlCommand("ServiceConfiguration_GetExecutionTime", sqlConnection))
						{
							sqlCommand.CommandType = System.Data.CommandType.StoredProcedure;

							sqlCommand.Parameters.AddWithValue("@ConfigName", configurationName);
							sqlCommand.Parameters.AddWithValue("@Percentile", percentile);
							sqlCommand.Parameters.AddWithValue("@ProfileID", accountID);

							averageExacutionTime = System.Convert.ToInt32(sqlCommand.ExecuteScalar());
							_servicePerProfileAvgExecutionTimeCash[key] = new ServicePerProfileAvgExecutionTimeCache() { AverageExecutionTime = averageExacutionTime, TimeSaved = DateTime.Now };



						}
					}
				}
			}
			catch
			{

				averageExacutionTime = 180;
			}
			return TimeSpan.FromMinutes(Math.Ceiling(TimeSpan.FromSeconds(averageExacutionTime).TotalMinutes));
		}

		/// <summary>
		/// if the schedule time is occupied then take first free time (minimum time)
		/// </summary>
		/// <param name="servicesWithSameProfile"></param>
		/// <param name="startTime"></param>
		/// <param name="endTime"></param>
		/// <param name="ExecutionTime"></param>
		private static void GetNewStartEndTime(IOrderedEnumerable<KeyValuePair<SchedulingInfo, ServiceInstance>> servicesWithSameProfile, ref DateTime startTime, ref DateTime endTime, TimeSpan ExecutionTime)
		{

			//startTime = servicesWithSameProfile.Min(s => s.Value.EndTime);
			DateTime calculatedStartTime = startTime;

			startTime = servicesWithSameProfile.Where(s => s.Value.EndTime >= calculatedStartTime).Min(s => s.Value.EndTime);
			if (startTime < DateTime.Now)
				startTime = DateTime.Now;

			//Get end time
			endTime = startTime.Add(ExecutionTime);
		}

		/// <summary>
		/// remove busy time 
		/// </summary>
		/// <param name="servicesWithSameConfiguration"></param>
		/// <param name="servicesWithSameProfile"></param>
		/// <param name="startTime"></param>
		private void RemoveBusyTime(ref IOrderedEnumerable<KeyValuePair<SchedulingInfo, ServiceInstance>> servicesWithSameConfiguration, ref IOrderedEnumerable<KeyValuePair<SchedulingInfo, ServiceInstance>> servicesWithSameProfile, DateTime startTime)
		{
			servicesWithSameConfiguration = from s in servicesWithSameConfiguration
											where s.Value.EndTime > startTime
											orderby s.Value.StartTime
											select s;

			servicesWithSameProfile = from s in servicesWithSameProfile
									  where s.Value.EndTime > startTime
									  orderby s.Value.StartTime
									  select s;
		}


		/// <summary>
		/// start the timers of new scheduling and services required to run
		/// </summary>
		public void Start()
		{
			Schedule(false);
			NotifyServicesToRun();


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

			_newSchedulethread.Start();
			_findRequiredServicesthread.Start();

		}

		private void NotifyServicesToRun()
		{
			//DO some checks
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
						// find unitialized services scheduled since the last interval
						//if (scheduleService.Value.StartTime > DateTime.Now - FindServicesToRunInterval-FindServicesToRunInterval &&
						//    scheduleService.Value.StartTime <= DateTime.Now &&
						//    scheduleService.Value.LegacyInstance.State == Legacy.ServiceState.Uninitialized)
						if (scheduleService.Value.StartTime <= DateTime.Now &&
							scheduleService.Key.TimeToRun.Add(scheduleService.Key.Rule.MaxDeviationAfter) >= DateTime.Now &&
						   scheduleService.Value.LegacyInstance.State == Legacy.ServiceState.Uninitialized)
						{
							instancesToRun.Add(scheduleService.Value);
						}
					}
				}
			}

			if (instancesToRun.Count > 0)
			{
				instancesToRun = (List<ServiceInstance>)instancesToRun.OrderBy(s => s.StartTime).ToList<ServiceInstance>();
				OnTimeToRun(new TimeToRunEventArgs() { ServicesToRun = instancesToRun.ToArray() });
			}
			instancesToRun.Clear();
		}

		/// <summary>
		///  stop the timers of new scheduling and services required to run
		/// </summary>
		public void Stop()
		{

			if (_findRequiredServicesthread != null)
				_findRequiredServicesthread.Abort();

			if (_newSchedulethread != null)
				_newSchedulethread.Abort();

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
			ServiceRunRequired(this, e);

		}

		/// <summary>
		/// set event new schedule created
		/// </summary>
		/// <param name="e"></param>
		public void OnNewScheduleCreated(ScheduledInformationEventArgs e)
		{
			NewScheduleCreated(this, e);
		}

		/// <summary>
		/// abort runing service
		/// </summary>
		/// <param name="SchedulingInfo"></param>
		public void AbortRuningService(SchedulingInfo SchedulingInfo)
		{
			_scheduledServices[SchedulingInfo]..Abort();
		}



		public List<ServiceConfiguration> GetAllExistServices()
		{
			List<ServiceConfiguration> allServices = new List<ServiceConfiguration>();
			Dictionary<string, ServiceConfiguration> baseConfigurations = new Dictionary<string, ServiceConfiguration>();
			Dictionary<string, ServiceConfiguration> configurations = new Dictionary<string, ServiceConfiguration>();
			//base configuration
			foreach (ServiceElement serviceElement in ServicesConfiguration.Services)
			{
				ServiceConfiguration serviceConfiguration = new ServiceConfiguration();
				serviceConfiguration.Name = serviceElement.Name;

				serviceConfiguration.MaxConcurrent = serviceElement.MaxInstances;
				serviceConfiguration.MaxCuncurrentPerProfile = serviceElement.MaxInstancesPerAccount;
				//serviceConfiguration.ID = GetServceConfigruationIDByName(serviceConfiguration.Name);
				baseConfigurations.Add(serviceConfiguration.Name, serviceConfiguration);
			}
			//profiles=account and specific aconfiguration
			foreach (AccountElement account in ServicesConfiguration.Accounts)
			{
				foreach (AccountServiceElement accountService in account.Services)
				{
					ServiceElement serviceUse = accountService.Uses.Element;
					//active element is the calculated configuration 
					ActiveServiceElement activeServiceElement = new ActiveServiceElement(accountService);
					ServiceConfiguration serviceConfiguration = new ServiceConfiguration();
					serviceConfiguration.Name = serviceUse.Name;
					if (activeServiceElement.Options.ContainsKey("ServicePriority"))
						serviceConfiguration.priority = int.Parse(activeServiceElement.Options["ServicePriority"]);

					serviceConfiguration.MaxConcurrent = (activeServiceElement.MaxInstances == 0) ? 9999 : activeServiceElement.MaxInstances;
					serviceConfiguration.MaxCuncurrentPerProfile = (activeServiceElement.MaxInstancesPerAccount == 0) ? 9999 : activeServiceElement.MaxInstancesPerAccount;
					serviceConfiguration.LegacyConfiguration = activeServiceElement;
					//scheduling rules 

					serviceConfiguration.BaseConfiguration = baseConfigurations[serviceUse.Name];
					//profile settings
					Profile profile = new Profile();
					profile.Name = account.ID.ToString();
					profile.ID = account.ID;
					profile.Settings = new Dictionary<string, object>();
					profile.Settings.Add("AccountID", account.ID);
					serviceConfiguration.SchedulingProfile = profile;
					allServices.Add(serviceConfiguration);
				}
			}
			return allServices.OrderBy(s => s.SchedulingProfile.Name).ToList();


		}
	}
	public class TimeToRunEventArgs : EventArgs
	{
		public ServiceInstance[] ServicesToRun;
	}
	public class ScheduledInformationEventArgs : EventArgs
	{
		public Dictionary<SchedulingInfo, ServiceInstance> ScheduleInformation;
		public Dictionary<SchedulingInfo, ServiceInstance> NotScheduledInformation;
	}
	public class ServicePerProfileAvgExecutionTimeCache
	{
		public long AverageExecutionTime { get; set; }
		public DateTime TimeSaved { get; set; }
	}
}
