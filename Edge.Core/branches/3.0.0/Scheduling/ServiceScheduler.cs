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
		public ServiceScheduler(bool getServicesFromConfigFile)
		{
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
			//set need reschedule to false in order to avoid more schedule from other threads
			_needReschedule = false;

			//Get next time line
			List<SchedulingInfo> servicesForNextTimeLine;
			if (reschedule && _timeLineFrom != DateTime.MinValue)
				servicesForNextTimeLine = GetServicesForNextTimeLine(true);
			else
				servicesForNextTimeLine = GetServicesForNextTimeLine(false);


			
			// TODO: improve sorting to include priority and AvergeExecutionTime
			List<SchedulingInfo> toBeScheduledByTimeAndPriority = servicesForNextTimeLine.OrderBy(s => s.TimeToRun).ThenByDescending(s => s.Priority).ToList();
			ClearServicesforReschedule(toBeScheduledByTimeAndPriority);
			lock (_scheduledServices)
			{
				foreach (SchedulingInfo SchedulingInfo in toBeScheduledByTimeAndPriority)
				{

					//if key exist then this service is runing or ednededule again
					if (!_scheduledServices.ContainsKey(SchedulingInfo))
					{
						//Get all services with same configurationID
						var servicesWithSameConfiguration = from s in _scheduledServices
															where s.Key.Configuration.ServiceName == SchedulingInfo.Configuration.BaseConfiguration.ServiceName && //should be id but no id yet
															s.Value.State != ServiceState.Ended
															orderby s.Value.StartTime ascending
															select s;

						//Get all services with same profileID

						var servicesWithSameProfile = from s in _scheduledServices
													  where s.Value.ProfileID == SchedulingInfo.Configuration.Profile.ID &&
													  s.Key.Configuration.ServiceName == SchedulingInfo.Configuration.BaseConfiguration.ServiceName &&
													  s.Value.State != ServiceState.Ended
													  orderby s.Value.StartTime ascending
													  select s;




						ServiceInstance serviceInstance = FindFirstFreeTime(servicesWithSameConfiguration, servicesWithSameProfile, SchedulingInfo);
						KeyValuePair<SchedulingInfo, ServiceInstance> serviceInstanceAndRuleHash = new KeyValuePair<SchedulingInfo, ServiceInstance>(SchedulingInfo, serviceInstance);


						if (serviceInstanceAndRuleHash.Key.ActualDeviation > serviceInstanceAndRuleHash.Key.Rule.MaxDeviationAfter && serviceInstanceAndRuleHash.Key.Rule.Scope != SchedulingScope.Unplanned)
						{
							// check if the waiting time is bigger then max waiting time.
							_unscheduleServices.Add(serviceInstanceAndRuleHash.Key, serviceInstanceAndRuleHash.Value);
							//Log.Write(this.ToString(), string.Format("Service {0} not schedule since it's scheduling exceed max MaxDeviation", serviceInstanceAndRuleHash.Value.ServiceName), LogMessageType.Warning);

						}
						else
						{
							_scheduledServices.Add(serviceInstanceAndRuleHash.Key, serviceInstanceAndRuleHash.Value);
						}


					}
				}
			}
			OnNewScheduleCreated(new ScheduledInformationEventArgs() { NotScheduledInformation = _unscheduleServices, ScheduleInformation = _scheduledServices });

		}



		/// <summary>
		/// Clear Services for reschedule them-it will only clean the services that is in the next time line.
		/// </summary>
		/// <param name="toBeScheduledByTimeAndPriority"></param>
		private void ClearServicesforReschedule(List<SchedulingInfo> toBeScheduledByTimeAndPriority)
		{
			lock (_scheduledServices)
			{
				foreach (SchedulingInfo SchedulingInfo in toBeScheduledByTimeAndPriority)
				{
					if (_scheduledServices.ContainsKey(SchedulingInfo)) //already scheduled
					{
						if (_scheduledServices[SchedulingInfo].State == ServiceState.Uninitialized) // and it's uninitalized then 
							_scheduledServices.Remove(SchedulingInfo);                                                    //remove it from scheduling data in order to schedule it again

					}
				}
				//Services that has ended and we want to see them for configured time 
				List<SchedulingInfo> endedAndTimeToClear = new List<SchedulingInfo>();

				foreach (var scheduledService in _scheduledServices)                   //but after configured time we want to clear them so..
				{
					if (scheduledService.Value.State == ServiceState.Ended)//if service ended 
					{
						// so if the difference between and time and now bigger or equal to configure time then remove it.
						endedAndTimeToClear.Add(scheduledService.Key);
					}
				}
				lock (_servicesWarehouse)
				{
					foreach (SchedulingInfo toClear in endedAndTimeToClear)
					{
						_scheduledServices.Remove(toClear); //clear from already schedule table
						toBeScheduledByTimeAndPriority.Remove(toClear); //clear from to be scheduled on the curent new schedule
						if (toClear.Rule.Scope == SchedulingScope.Unplanned)
							_servicesWarehouse.Remove(toClear.Configuration); //clear from services in services wherhouse(all services from configuration and unplaned)
					}
				}


				lock (_unscheduleServices)
				{


					_unscheduleServices.Clear();

					//services that did not run because their base time + maxdiviation<datetime.now 
					//should have been run but from some reason did not run
					foreach (KeyValuePair<SchedulingInfo, ServiceInstance> scheduldService in _scheduledServices)
					{

						if (scheduldService.Key.TimeToRun.Add(scheduldService.Key.Rule.MaxDeviationAfter) > DateTime.Now && scheduldService.Value.State == ServiceState.Uninitialized)
							_unscheduleServices.Add(scheduldService.Key, scheduldService.Value);

					}
					//clar the services that will not be run
					foreach (KeyValuePair<SchedulingInfo, ServiceInstance> unScheduledService in _unscheduleServices)
					{
						_scheduledServices.Remove(unScheduledService.Key);
					}
				}


			}


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
		private List<SchedulingInfo> GetServicesForNextTimeLine(bool reschedule)
		{
			if (!reschedule)
			{


				_timeLineFrom = DateTime.Now;
				_timeLineTo = DateTime.Now.Add(_neededScheduleTimeLine);
			}
			List<SchedulingInfo> SchedulingInfo = FindSuitableSchedulingRule();
			return SchedulingInfo;
		}

		/// <summary>
		/// return a services that are suitable for the given  time line
		/// </summary>
		/// <returns></returns>
		private List<SchedulingInfo> FindSuitableSchedulingRule()
		{
			SchedulingInfo SchedulingInfo;
			List<SchedulingInfo> foundedSchedulingInfo = new List<SchedulingInfo>();

			lock (_servicesWarehouse)
			{
				for (int i = 0; i < _servicesWarehouse.Count; i++)
				{
					foreach (SchedulingRule schedulingRule in _servicesWarehouse[i].SchedulingRules)
					{
						ServiceConfiguration service = _servicesWarehouse[i];
						if (schedulingRule != null)
						{
							foreach (TimeSpan hour in schedulingRule.Hours)
							{

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
													 NewMethod(ref foundedSchedulingInfo, schedulingRule, service,  hour, timeToRun);

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
														SchedulingInfo = new SchedulingInfo() { Configuration = service, Rule = schedulingRule, SelectedDay = (int)(DateTime.Now.DayOfWeek) + 1, SelectedHour = hour, TimeToRun = timeToRunFrom };
														if (!CheckIfSpecificSchedulingRuleDidNotRunYet(SchedulingInfo))
															foundedSchedulingInfo.Add(SchedulingInfo);

													}

												}
												if (day == (int)timeToRunTo.DayOfWeek + 1)
												{
													if (timeToRunTo >= _timeLineFrom && timeToRunTo <= _timeLineTo || timeToRunTo <= _timeLineFrom && timeToRunTo.Add(schedulingRule.MaxDeviationAfter) >= DateTime.Now)
													{
														SchedulingInfo = new SchedulingInfo() { Configuration = service, Rule = schedulingRule, SelectedDay = (int)(DateTime.Now.DayOfWeek) + 1, SelectedHour = hour, TimeToRun = timeToRunTo };
														if (!CheckIfSpecificSchedulingRuleDidNotRunYet(SchedulingInfo))
															foundedSchedulingInfo.Add(SchedulingInfo);

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
														SchedulingInfo = new SchedulingInfo() { Configuration = service, Rule = schedulingRule, SelectedDay = (int)(DateTime.Now.DayOfWeek) + 1, SelectedHour = hour, TimeToRun = timeToRunFrom };
														if (!CheckIfSpecificSchedulingRuleDidNotRunYet(SchedulingInfo))
															foundedSchedulingInfo.Add(SchedulingInfo);

													}
												}
												if (day == (int)timeToRunTo.DayOfWeek)
												{
													if (timeToRunTo >= _timeLineFrom && timeToRunTo <= _timeLineTo || timeToRunTo <= _timeLineFrom && timeToRunTo.Add(schedulingRule.MaxDeviationAfter) >= DateTime.Now)
													{
														SchedulingInfo = new SchedulingInfo() { Configuration = service, Rule = schedulingRule, SelectedDay = (int)(DateTime.Now.DayOfWeek) + 1, SelectedHour = hour, TimeToRun = timeToRunTo };
														if (!CheckIfSpecificSchedulingRuleDidNotRunYet(SchedulingInfo))
															foundedSchedulingInfo.Add(SchedulingInfo);

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
												SchedulingInfo = new SchedulingInfo() { Configuration = service, Rule = schedulingRule, SelectedDay = (int)(DateTime.Now.DayOfWeek) + 1, SelectedHour = hour, TimeToRun = timeToRun };
												if (!CheckIfSpecificSchedulingRuleDidNotRunYet(SchedulingInfo))
												{
													foundedSchedulingInfo.Add(SchedulingInfo);

												}

											}
											break;
										}
								}

							}
						}
					}
				}

			}
			return foundedSchedulingInfo;
		}
		/// <summary>
		/// Add suitable SchedulingInfo to a list of suitable schedulingInfo
		/// </summary>
		/// <param name="foundedSchedulingInfo"></param>
		/// <param name="schedulingRule"></param>
		/// <param name="service"></param>
		/// <param name="hour"></param>
		/// <param name="timeToRun"></param>
		private void AddSuitableSchedulingInfo(ref List<SchedulingInfo> foundedSchedulingInfo, SchedulingRule schedulingRule, ServiceConfiguration service, TimeSpan hour, DateTime timeToRun)
		{
			SchedulingInfo SchedulingInfo;
			SchedulingInfo = new SchedulingInfo() { Configuration = service, Rule = schedulingRule, SelectedDay = (int)(DateTime.Now.DayOfWeek) + 1, SelectedHour = hour, TimeToRun = timeToRun };
			if (!CheckIfSpecificSchedulingRuleDidNotRunYet(SchedulingInfo))
				foundedSchedulingInfo.Add(SchedulingInfo);
			
		}

		private bool CheckIfSpecificSchedulingRuleDidNotRunYet(SchedulingInfo SchedulingInfo)
		{
			//TODO: CHECK ON THE DATABASE THAT SERVICE DID NOT RUN YET
			return false; //NOT RUN YET
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
		/// set the service instance on the right time get the service instance with all the data of scheduling and more
		/// </summary>
		/// <param name="serviceInstanceAndRuleHash"></param>
		private void UpdateScheduleTable(KeyValuePair<SchedulingInfo, ServiceInstance> serviceInstanceAndRuleHash)
		{

		}



		/// <summary>
		/// Schedule per service
		/// </summary>
		/// <param name="SchedulingInfo"></param>
		/// <returns>service instance with scheduling data and more</returns>
		private ServiceInstance ScheduleSpecificService(SchedulingInfo SchedulingInfo)
		{
			//Get all services with same configurationID
			var servicesWithSameConfiguration = from s in _scheduledServices
												where s.Key.Configuration.ServiceName == SchedulingInfo.Configuration.BaseConfiguration.ServiceName && //should be id but no id yet
												s.Value.State != ServiceState.Ended
												orderby s.Value.StartTime ascending
												select s;

			//Get all services with same profileID

			var servicesWithSameProfile = from s in _scheduledServices
										  where s.Value.ProfileID == SchedulingInfo.Configuration.Profile.ID &&
										  s.Key.Configuration.ServiceName == SchedulingInfo.Configuration.BaseConfiguration.ServiceName &&
										  s.Value.State != ServiceState.Ended
										  orderby s.Value.StartTime ascending
										  select s;




			ServiceInstance serviceInstance = FindFirstFreeTime(servicesWithSameConfiguration, servicesWithSameProfile, SchedulingInfo);

			return serviceInstance;
		}

		/// <summary>
		/// The algoritm of finding the the right time for service
		/// </summary>
		/// <param name="servicesWithSameConfiguration"></param>
		/// <param name="servicesWithSameProfile"></param>
		/// <param name="SchedulingInfo"></param>
		/// <returns></returns>
		private ServiceInstance FindFirstFreeTime(IOrderedEnumerable<KeyValuePair<SchedulingInfo, ServiceInstance>> servicesWithSameConfiguration, IOrderedEnumerable<KeyValuePair<SchedulingInfo, ServiceInstance>> servicesWithSameProfile, SchedulingInfo SchedulingInfo)
		{
			ServiceInstance serviceInstacnce = null;
			TimeSpan executionTimeInSeconds = GetAverageExecutionTime(SchedulingInfo.Configuration.ServiceName, SchedulingInfo.Configuration.Profile.ID, _percentile);

			DateTime baseStartTime = (SchedulingInfo.TimeToRun < DateTime.Now) ? DateTime.Now : SchedulingInfo.TimeToRun;
			DateTime baseEndTime = baseStartTime.Add(executionTimeInSeconds);
			DateTime calculatedStartTime = baseStartTime;
			DateTime calculatedEndTime = baseEndTime;
			bool found = false;


			while (!found)
			{
				int countedPerConfiguration = servicesWithSameConfiguration.Count(s => (calculatedStartTime >= s.Value.StartTime && calculatedStartTime <= s.Value.EndTime) || (calculatedEndTime >= s.Value.StartTime && calculatedEndTime <= s.Value.EndTime));
				if (countedPerConfiguration < SchedulingInfo.Configuration.Limits.MaxConcurrentGlobal)
				{
					int countedPerProfile = servicesWithSameProfile.Count(s => (calculatedStartTime >= s.Value.StartTime && calculatedStartTime <= s.Value.EndTime) || (calculatedEndTime >= s.Value.StartTime && calculatedEndTime <= s.Value.EndTime));
					if (countedPerProfile < SchedulingInfo.Configuration.Limits.MaxConcurrentPerProfile)
					{
						serviceInstacnce = new ServiceInstance();
						serviceInstacnce.StartTime = calculatedStartTime;
						serviceInstacnce.EndTime = calculatedEndTime;
						serviceInstacnce.Odds = _percentile;
						serviceInstacnce.ActualDeviation = calculatedStartTime.Subtract(SchedulingInfo.TimeToRun);
						serviceInstacnce.Priority = SchedulingInfo.Priority;
						serviceInstacnce.BaseConfigurationID = SchedulingInfo.Configuration.BaseConfiguration.ID;
						serviceInstacnce.ID = SchedulingInfo.Configuration.ID;
						serviceInstacnce.MaxConcurrentPerConfiguration = SchedulingInfo.Configuration.MaxConcurrent;
						serviceInstacnce.MaxCuncurrentPerProfile = SchedulingInfo.Configuration.MaxCuncurrentPerProfile;
						serviceInstacnce.MaxDeviationAfter = SchedulingInfo.Rule.MaxDeviationAfter;
						serviceInstacnce.ActualDeviation = calculatedStartTime.Subtract(SchedulingInfo.TimeToRun);
						serviceInstacnce.MaxDeviationBefore = SchedulingInfo.Rule.MaxDeviationBefore;
						serviceInstacnce.ProfileID = SchedulingInfo.Configuration.SchedulingProfile.ID;
						serviceInstacnce.LegacyInstance = Legacy.Service.CreateInstance(SchedulingInfo.LegacyConfiguration, serviceInstacnce.ProfileID);
						serviceInstacnce.LegacyInstance.StateChanged += new EventHandler(LegacyInstance_StateChanged);
						serviceInstacnce.LegacyInstance.TimeScheduled = calculatedStartTime;
						serviceInstacnce.ServiceName = SchedulingInfo.Configuration.Name;
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
					using (SqlConnection sqlConnection=new SqlConnection(connectionString))
					{
						using (SqlCommand sqlCommand =new SqlCommand("ServiceConfiguration_GetExecutionTime",sqlConnection))
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
		/// Print the schedule
		/// </summary>
		private void PrintSchduleTable()
		{
			Console.WriteLine("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}", "SchedulidDataID", "Name".PadRight(25, ' '), "start", "end", "diff", "odds", "priority");
			Console.WriteLine("---------------------------------------------------------");
			KeyValuePair<SchedulingInfo, ServiceInstance>? prev = null;
			foreach (KeyValuePair<SchedulingInfo, ServiceInstance> scheduled in _scheduledServices.OrderBy(s => s.Value.StartTime))
			{
				Console.WriteLine("{0}\t{1:HH:mm}\t{2:HH:mm}\t{3:hh\\:mm}\t{4}\t{5}\t{6}",
					scheduled.Key.GetHashCode(),
					scheduled.Value.ServiceName.PadRight(25, ' '),
					scheduled.Value.StartTime,
					scheduled.Value.EndTime,
					/*prev != null ? scheduled.Value.StartTime - prev.Value.Value.EndTime : TimeSpan.FromMinutes(0)*/
					scheduled.Value.StartTime.Subtract(scheduled.Value.EndTime),
					Math.Round(scheduled.Value.Odds, 2),
					scheduled.Value.Priority);
				prev = scheduled;
			}
			if (_unscheduleServices.Count > 0)
				Console.WriteLine("---------------------Will not be scheduled--------------------------------------");
			foreach (KeyValuePair<SchedulingInfo, ServiceInstance> notScheduled in _unscheduleServices)
			{
				Console.WriteLine("Service name: {0}\tBase start time:{1:hh:mm}\tschedule time is{2:hh:mm}\t maximum waiting time is{3}", notScheduled.Value.ServiceName, notScheduled.Value.EndTime, notScheduled.Value.StartTime, notScheduled.Value.MaxDeviationAfter);
			}
			Console.ReadLine();
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
