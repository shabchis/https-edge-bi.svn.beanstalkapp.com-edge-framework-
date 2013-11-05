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
using System.ComponentModel;





namespace Edge.Core.Scheduling
{

	/// <summary>
	/// The new scheduler
	/// </summary>
	public class Scheduler
	{
		#region members
		private List<ServiceConfiguration> _servicesWarehouse = new List<ServiceConfiguration>(); //all services from configuration file load to this var
		private Dictionary<SchedulingData, ServiceInstance> _scheduledServices = new Dictionary<SchedulingData, ServiceInstance>();
		private Dictionary<int, ServiceConfiguration> _servicesPerConfigurationID = new Dictionary<int, ServiceConfiguration>();
		private Dictionary<int, ServiceConfiguration> _servicesPerProfileID = new Dictionary<int, ServiceConfiguration>();
		private Dictionary<SchedulingData, ServiceInstance> _mayNotBescheduleServices = new Dictionary<SchedulingData, ServiceInstance>();
		private Dictionary<string, ServicePerProfileAvgExecutionTimeCash> _servicePerProfileAvgExecutionTimeCash = new Dictionary<string, ServicePerProfileAvgExecutionTimeCash>();
		DateTime _timeLineFrom;
		DateTime _timeLineTo;
		private TimeSpan _neededScheduleTimeLine; //scheduling for the next xxx min....
		private int _percentile = 80; //execution time of specifc service on sprcific Percentile
		private TimeSpan _intervalNewSchedule;
		private TimeSpan _intervalfindServicesToRun;
		private TimeSpan _timeToDeleteServiceFromTimeLine;
		public event EventHandler ServiceRunRequiredEvent;
		public event EventHandler NewScheduleCreatedEvent;
		volatile bool _needReschedule = false;
		TimeSpan _executionTimeCashTimeOutAfter;
		object _sync;
		SchedulerState _state = SchedulerState.Stopped;

		private BackgroundWorker _timers;
	
		#endregion

		public event EventHandler StateChanged;

		/// <summary>
		/// Initialize all the services from configuration file or db4o
		/// </summary>
		/// <param name="getServicesFromConfigFile"></param>
		public Scheduler(bool getServicesFromConfigFile)
		{
			if (getServicesFromConfigFile)
				GetServicesFromConfigurationFile();


			_percentile = int.Parse(AppSettings.Get(this, "Percentile"));
			_neededScheduleTimeLine = TimeSpan.Parse(AppSettings.Get(this, "NeededScheduleTimeLine"));
			_intervalNewSchedule = TimeSpan.Parse(AppSettings.Get(this, "IntervalBetweenNewSchedule"));
			_intervalfindServicesToRun = TimeSpan.Parse(AppSettings.Get(this, "FindServicesToRunInterval"));
			_timeToDeleteServiceFromTimeLine = TimeSpan.Parse(AppSettings.Get(this, "DeleteEndedServiceInterval"));
			_executionTimeCashTimeOutAfter = TimeSpan.Parse(AppSettings.Get(this, "DeleteEndedServiceInterval"));
			_sync = new object();
		}

		public SchedulerState State
		{
			get { return _state; }
			set
			{
				_state = value;
				if (this.StateChanged != null)
					this.StateChanged(this, EventArgs.Empty);
			}
		}

		/// <summary>
		/// start the timers of new scheduling and services required to run
		/// </summary>
		public void Start()
		{
			if (this.State != SchedulerState.Stopped)
				return;

			this.State = SchedulerState.Starting;
			Schedule(false);
			NotifyServicesToRun();

			_timers = new BackgroundWorker()
			{
				WorkerReportsProgress = false,
				WorkerSupportsCancellation = true
			};
			_timers.DoWork += new DoWorkEventHandler((worker, args) =>
			{
				this.State = SchedulerState.Started;
				DateTime last = DateTime.Now;
				while (!((BackgroundWorker)worker).CancellationPending)
				{
					if (last - DateTime.Now >= _intervalNewSchedule)
						Schedule(false);
					if (last - DateTime.Now >= _intervalfindServicesToRun)
						NotifyServicesToRun();
					last = DateTime.Now;
					Thread.Sleep(TimeSpan.FromSeconds(1));
				}
			});
			_timers.RunWorkerCompleted += new RunWorkerCompletedEventHandler((worker, args) =>
			{
				this.State = SchedulerState.Stopped;
				_timers.Dispose();
			});
			_timers.RunWorkerAsync();
		}

		/// <summary>
		///  stop the timers of new scheduling and services required to run
		/// </summary>
		public void Stop()
		{
			this.State = SchedulerState.Stopping;
			_timers.CancelAsync();
		}


		/// <summary>
		/// The main method of creating scheduler 
		/// </summary>
		public void Schedule(bool reschedule = false)
		{
			lock (_servicesWarehouse)
			{
				lock (_scheduledServices)
				{
					//set need reschedule to false in order to avoid more schedule from other threads
					_needReschedule = false;
					//Get next time line
					List<SchedulingData> servicesForNextTimeLine = GetServicesForNextTimeLine(reschedule);
					//sort by time to run and priority
					List<SchedulingData> toBeScheduledByTimeAndPriority = servicesForNextTimeLine.OrderBy(s => s.TimeToRun).ThenByDescending(s => s.Priority).ToList();
					#region CleanUp
					// check if the scheduling info is not already in use - if it is, remove it in order to reschedule in the current operation
					foreach (SchedulingData schedulingData in toBeScheduledByTimeAndPriority)
					{
						if (_scheduledServices.ContainsKey(schedulingData))
						{
							//not tag as deleted
							if (_scheduledServices[schedulingData].Deleted == false)
							{
								// and it's uninitalized then 
								if (_scheduledServices[schedulingData].LegacyInstance.State == Legacy.ServiceState.Uninitialized)
									_scheduledServices.Remove(schedulingData);
							}
						}


					}
					//Services that has ended and we want to see them for configured time 
					List<SchedulingData> endedAndTimeToClear = new List<SchedulingData>();
					foreach (var scheduledService in _scheduledServices)
					{
						//if service ended 
						if (scheduledService.Value.LegacyInstance.State == Legacy.ServiceState.Ended ||
							//or aborting  or tag as delted
							scheduledService.Value.LegacyInstance.State == Legacy.ServiceState.Aborting || scheduledService.Value.Deleted == true)
						{
							// if the difference between and time and now bigger or equal to configure time then remove it.
							if (scheduledService.Value.LegacyInstance.TimeEnded + _timeToDeleteServiceFromTimeLine < DateTime.Now)
								endedAndTimeToClear.Add(scheduledService.Key);
						}
					}

					foreach (SchedulingData toClear in endedAndTimeToClear)
					{
						//clear from already schedule table
						_scheduledServices.Remove(toClear);
						//clear from to be scheduled on the curent new schedule
						toBeScheduledByTimeAndPriority.Remove(toClear);
						//clear from services in services wherhouse(all services from configuration and unplaned)
						if (toClear.Rule.Scope == SchedulingScope.UnPlanned)
							_servicesWarehouse.Remove(toClear.Configuration);
					}

					//clear unscheduled
					_mayNotBescheduleServices.Clear();
					//services that did not run because their base time + maxdiviation<datetime.now 
					foreach (KeyValuePair<SchedulingData, ServiceInstance> scheduldService in _scheduledServices)
					{
						if (scheduldService.Key.TimeToRun.Add(scheduldService.Key.Rule.MaxDeviationAfter) > DateTime.Now && scheduldService.Value.LegacyInstance.State == Legacy.ServiceState.Uninitialized)
							_mayNotBescheduleServices.Add(scheduldService.Key, scheduldService.Value);

					}
					foreach (KeyValuePair<SchedulingData, ServiceInstance> unScheduledService in _mayNotBescheduleServices) //clar the services that will not be run
					{
						_scheduledServices.Remove(unScheduledService.Key);
					}





					#endregion

					#region Find Match services
					//Same services or same services with same profile

					foreach (SchedulingData schedulingData in toBeScheduledByTimeAndPriority)
					{

						//if key exist then this service is runing or ended and should nt be schedule again
						if (!_scheduledServices.ContainsKey(schedulingData))
						{

							//Get all services with same configurationID
							var servicesWithSameConfiguration = from s in _scheduledServices
																where s.Key.Configuration.Name == schedulingData.Configuration.BaseConfiguration.Name && //should be id but no id yet
																s.Value.LegacyInstance.State != Legacy.ServiceState.Ended &&
																s.Value.Deleted == false //runnig or not started yet
																orderby s.Value.StartTime ascending
																select s;

							//Get all services with same profileID

							var servicesWithSameProfile = from s in _scheduledServices
														  where s.Value.ProfileID == schedulingData.Configuration.SchedulingProfile.ID &&
														  s.Key.Configuration.Name == schedulingData.Configuration.BaseConfiguration.Name &&
														  s.Value.LegacyInstance.State != Legacy.ServiceState.Ended &&
														  s.Value.Deleted == false //not deleted
														  orderby s.Value.StartTime ascending
														  select s;

							#region FindFirstFreeTimeForTheService
							//Find the first available time this service with specific service and profile
							ServiceInstance serviceInstance = null;
							TimeSpan executionTimeInSeconds = GetAverageExecutionTime(schedulingData.Configuration.Name, schedulingData.Configuration.SchedulingProfile.ID, _percentile);

							DateTime baseStartTime = (schedulingData.TimeToRun < DateTime.Now) ? DateTime.Now : schedulingData.TimeToRun;
							DateTime baseEndTime = baseStartTime.Add(executionTimeInSeconds);
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
										serviceInstance = new ServiceInstance();
										serviceInstance.StartTime = calculatedStartTime;
										serviceInstance.EndTime = calculatedEndTime;
										serviceInstance.Odds = _percentile;
										serviceInstance.ActualDeviation = calculatedStartTime.Subtract(schedulingData.TimeToRun);
										serviceInstance.Priority = schedulingData.Priority;
										serviceInstance.BaseConfigurationID = schedulingData.Configuration.BaseConfiguration.ID;
										serviceInstance.ID = schedulingData.Configuration.ID;
										serviceInstance.MaxConcurrentPerConfiguration = schedulingData.Configuration.MaxConcurrent;
										serviceInstance.MaxCuncurrentPerProfile = schedulingData.Configuration.MaxConcurrentPerProfile;
										serviceInstance.MaxDeviationAfter = schedulingData.Rule.MaxDeviationAfter;
										serviceInstance.ActualDeviation = calculatedStartTime.Subtract(schedulingData.TimeToRun);
										serviceInstance.MaxDeviationBefore = schedulingData.Rule.MaxDeviationBefore;
										serviceInstance.ProfileID = schedulingData.Configuration.SchedulingProfile.ID;
										if (schedulingData.Configuration.Instance == null)
											serviceInstance.LegacyInstance = Legacy.Service.CreateInstance(schedulingData.LegacyConfiguration, serviceInstance.ProfileID);
										else
											serviceInstance.LegacyInstance = schedulingData.Configuration.Instance;
										serviceInstance.LegacyInstance.StateChanged += new EventHandler<Legacy.ServiceStateChangedEventArgs>(LegacyInstance_StateChanged);
										serviceInstance.LegacyInstance.TimeScheduled = calculatedStartTime;
										serviceInstance.ServiceName = schedulingData.Configuration.Name;
										found = true;
									}
									else
									{
										calculatedStartTime = servicesWithSameProfile.Where(s => s.Value.EndTime >= calculatedStartTime).Min(s => s.Value.EndTime);
										if (calculatedStartTime < DateTime.Now)
											calculatedStartTime = DateTime.Now;

										//Get end time
										calculatedEndTime = calculatedStartTime.Add(executionTimeInSeconds);
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
									calculatedEndTime = calculatedStartTime.Add(executionTimeInSeconds);
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
							if (serviceInstanceAndSchedulingRule.Value.ActualDeviation > serviceInstanceAndSchedulingRule.Value.MaxDeviationAfter && serviceInstanceAndSchedulingRule.Key.Rule.Scope != SchedulingScope.UnPlanned)
							{
								// check if the waiting time is bigger then max waiting time.
								_mayNotBescheduleServices.Add(serviceInstanceAndSchedulingRule.Key, serviceInstanceAndSchedulingRule.Value);
								if (DateTime.Now > serviceInstanceAndSchedulingRule.Key.TimeToRun + serviceInstanceAndSchedulingRule.Value.MaxDeviationAfter)
									Log.Write(this.ToString(), string.Format("Service {0} may not schedule since it's scheduling exceed max MaxDeviation", serviceInstanceAndSchedulingRule.Value.ServiceName), LogMessageType.Warning);

							}
							else
							{
								_scheduledServices.Add(serviceInstanceAndSchedulingRule.Key, serviceInstanceAndSchedulingRule.Value);
							}
							#endregion
						}



					}

					#endregion

					#region For Debug
					//try
					//{
					//    StringBuilder str = new StringBuilder();
					//    using (StreamWriter writer = new StreamWriter(Path.Combine(AppSettings.Get("",""),string.Format("{0}.csv",DateTime.Now.ToString("dd-MM-yyyy--HH:mm")))))
					//    {
					//        foreach (var schedule in _scheduledServices)
					//        {

					//        }



					//    }


					//}
					//catch (Exception)
					//{


					//}

					#endregion
					OnNewScheduleCreated(new ScheduledInformationEventArgs() { NotScheduledInformation = _mayNotBescheduleServices, ScheduleInformation = _scheduledServices });


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
		public IEnumerable<KeyValuePair<SchedulingData, ServiceInstance>> GetAllScheduledServices()
		{
			var returnObject = from s in _scheduledServices
							   select s;
			return returnObject;
		}

		/// <summary>
		/// add unplanned service to schedule
		/// </summary>
		/// <param name="serviceConfiguration"></param>
		public void AddNewServiceToSchedule(ServiceConfiguration serviceConfiguration)
		{
			lock (this)
			{
				_servicesWarehouse.Add(serviceConfiguration);
			}

			_needReschedule = true;
		}
		public void AddChildServiceToSchedule(Legacy.ServiceInstance serviceInstance)
		{
			lock (_servicesWarehouse)
			{
				ServiceElement baseService = EdgeServicesConfiguration.Current.Services[serviceInstance.Configuration.Name];
				ServiceConfiguration baseConfiguration = new ServiceConfiguration();
				baseConfiguration.MaxConcurrent = baseService.MaxInstances;
				baseConfiguration.MaxConcurrentPerProfile = baseService.MaxInstancesPerAccount;
				baseConfiguration.Name = baseService.Name;

				AccountElement accountElement = EdgeServicesConfiguration.Current.Accounts.GetAccount(serviceInstance.AccountID);

				ServiceConfiguration serviceConfiguration = new ServiceConfiguration();
				serviceConfiguration.BaseConfiguration = baseConfiguration;
				serviceConfiguration.Name = serviceInstance.Configuration.Name;
				serviceConfiguration.MaxConcurrentPerProfile = serviceInstance.Configuration.MaxInstancesPerAccount;
				serviceConfiguration.MaxConcurrent = serviceInstance.Configuration.MaxInstances;
				serviceConfiguration.LegacyConfiguration = serviceInstance.Configuration;
				serviceConfiguration.Instance = serviceInstance;
				serviceConfiguration.SchedulingRules.Add(new SchedulingRule() { Scope = SchedulingScope.UnPlanned, MaxDeviationAfter = new TimeSpan(0, 3, 0), Days = new List<int>(), Hours = new List<TimeSpan>() { new TimeSpan(0, 0, 0, 0) }, GuidForUnplaned = Guid.NewGuid(), SpecificDateTime = DateTime.Now });
				Profile profile = new Profile();
				profile.ID = accountElement.ID;
				profile.Name = accountElement.Name;
				profile.Settings = new Dictionary<string, object>();

				profile.Settings.Add("AccountID", accountElement.ID.ToString());
				serviceConfiguration.SchedulingProfile = profile;

				_servicesWarehouse.Add(serviceConfiguration);


				Schedule(true);
				NotifyServicesToRun();
			}


		}


		/// <summary>
		/// Get this time line services 
		/// </summary>
		/// <param name="reschedule">if it's for reschedule then the time line is the same as the last schedule</param>
		/// <returns></returns>
		private List<SchedulingData> GetServicesForNextTimeLine(bool reschedule)
		{
			if (!reschedule)
			{


				_timeLineFrom = DateTime.Now;
				_timeLineTo = DateTime.Now.Add(_neededScheduleTimeLine);
			}
			SchedulingData schedulingdata;
			List<SchedulingData> foundedSchedulingdata = new List<SchedulingData>();
			List<SchedulingData> willNotRun = new List<SchedulingData>();
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
													schedulingdata = new SchedulingData() { Configuration = service, profileID = service.SchedulingProfile.ID, Rule = schedulingRule, SelectedDay = (int)(DateTime.Now.DayOfWeek) + 1, SelectedHour = hour, Priority = service.priority, LegacyConfiguration = service.LegacyConfiguration, TimeToRun = timeToRun };
													foundedSchedulingdata.Add(schedulingdata);

												}
												else //Check if it already been trying to schedule then it it should be error
												{
													schedulingdata = new SchedulingData() { Configuration = service, profileID = service.SchedulingProfile.ID, Rule = schedulingRule, SelectedDay = (int)(DateTime.Now.DayOfWeek) + 1, SelectedHour = hour, Priority = service.priority, LegacyConfiguration = service.LegacyConfiguration, TimeToRun = timeToRun };
													if (_scheduledServices.ContainsKey(schedulingdata) && _scheduledServices[schedulingdata].LegacyInstance.State == Legacy.ServiceState.Uninitialized)
													{
														//Log.Write(this.ToString(), string.Format("Service {0} will not run!! since it's scheduling exceed max MaxDeviation", schedulingdata.Configuration.Name), new Exception("Service will not run!"), LogMessageType.Error);
														willNotRun.Add(schedulingdata);
													}

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
														schedulingdata = new SchedulingData() { Configuration = service, profileID = service.SchedulingProfile.ID, Rule = schedulingRule, SelectedDay = (int)(DateTime.Now.DayOfWeek) + 1, SelectedHour = hour, Priority = service.priority, LegacyConfiguration = service.LegacyConfiguration, TimeToRun = timeToRunFrom };
														foundedSchedulingdata.Add(schedulingdata);
													}

												}
												else if (day == (int)timeToRunTo.DayOfWeek + 1)
												{
													if (timeToRunTo >= _timeLineFrom && timeToRunTo <= _timeLineTo || timeToRunTo <= _timeLineFrom && timeToRunTo.Add(schedulingRule.MaxDeviationAfter) >= DateTime.Now)
													{
														schedulingdata = new SchedulingData() { Configuration = service, profileID = service.SchedulingProfile.ID, Rule = schedulingRule, SelectedDay = (int)(DateTime.Now.DayOfWeek) + 1, SelectedHour = hour, Priority = service.priority, LegacyConfiguration = service.LegacyConfiguration, TimeToRun = timeToRunTo };
														foundedSchedulingdata.Add(schedulingdata);

													}
												}
												else //Check if it already been trying to schedule then it it should be error
												{
													schedulingdata = new SchedulingData() { Configuration = service, profileID = service.SchedulingProfile.ID, Rule = schedulingRule, SelectedDay = (int)(DateTime.Now.DayOfWeek) + 1, SelectedHour = hour, Priority = service.priority, LegacyConfiguration = service.LegacyConfiguration, TimeToRun = timeToRunTo };
													if (_scheduledServices.ContainsKey(schedulingdata) && _scheduledServices[schedulingdata].LegacyInstance.State == Legacy.ServiceState.Uninitialized)
													{
														//Log.Write(this.ToString(), string.Format("Service {0} will not run!! since it's scheduling exceed max MaxDeviation", schedulingdata.Configuration.Name), new Exception("Service will not run!"), LogMessageType.Error);
														willNotRun.Add(schedulingdata);
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
														schedulingdata = new SchedulingData() { Configuration = service, profileID = service.SchedulingProfile.ID, Rule = schedulingRule, SelectedDay = (int)(DateTime.Now.DayOfWeek) + 1, SelectedHour = hour, Priority = service.priority, LegacyConfiguration = service.LegacyConfiguration, TimeToRun = timeToRunFrom };
														foundedSchedulingdata.Add(schedulingdata);

													}
												}
												else if (day == (int)timeToRunTo.DayOfWeek)
												{
													if (timeToRunTo >= _timeLineFrom && timeToRunTo <= _timeLineTo || timeToRunTo <= _timeLineFrom && timeToRunTo.Add(schedulingRule.MaxDeviationAfter) >= DateTime.Now)
													{
														schedulingdata = new SchedulingData() { Configuration = service, profileID = service.SchedulingProfile.ID, Rule = schedulingRule, SelectedDay = (int)(DateTime.Now.DayOfWeek) + 1, SelectedHour = hour, Priority = service.priority, LegacyConfiguration = service.LegacyConfiguration, TimeToRun = timeToRunTo };
														foundedSchedulingdata.Add(schedulingdata);

													}
												}
												else //Check if it already been trying to schedule then it it should be error
												{
													schedulingdata = new SchedulingData() { Configuration = service, profileID = service.SchedulingProfile.ID, Rule = schedulingRule, SelectedDay = (int)(DateTime.Now.DayOfWeek) + 1, SelectedHour = hour, Priority = service.priority, LegacyConfiguration = service.LegacyConfiguration, TimeToRun = timeToRunTo };
													if (_scheduledServices.ContainsKey(schedulingdata) && _scheduledServices[schedulingdata].LegacyInstance.State == Legacy.ServiceState.Uninitialized)
													{
														//Log.Write(this.ToString(), string.Format("Service {0} will not run!! since it's scheduling exceed max MaxDeviation", schedulingdata.Configuration.Name), new Exception("Service will not run!"), LogMessageType.Error);
														willNotRun.Add(schedulingdata);
													}

												}
											}
											break;
										}
									case SchedulingScope.UnPlanned:
										{

											DateTime timeToRun = schedulingRule.SpecificDateTime;
											if (timeToRun >= _timeLineFrom && timeToRun <= _timeLineTo || timeToRun <= _timeLineFrom && timeToRun.Add(schedulingRule.MaxDeviationAfter) > DateTime.Now)
											{
												schedulingdata = new SchedulingData() { Configuration = service, profileID = service.SchedulingProfile.ID, Rule = schedulingRule, SelectedDay = (int)(DateTime.Now.DayOfWeek) + 1, SelectedHour = hour, Priority = service.priority, LegacyConfiguration = service.LegacyConfiguration, TimeToRun = timeToRun, Guid = schedulingRule.GuidForUnplaned };
												schedulingdata.Rule.MaxDeviationAfter = new TimeSpan(8, 0, 0);
												foundedSchedulingdata.Add(schedulingdata);
											}
											break;
										}
								}

							}
						}
					}
				}

			}
			//if (willNotRun.Count > 0)
			//	NotRunServices(this, new WillNotRunEventArgs() { WillNotRun = willNotRun });

			return foundedSchedulingdata;
		}

		/// <summary>
		/// Load and translate the services from app.config
		/// </summary>
		private void GetServicesFromConfigurationFile()
		{
			Dictionary<string, ServiceConfiguration> baseConfigurations = new Dictionary<string, ServiceConfiguration>();
			Dictionary<string, ServiceConfiguration> configurations = new Dictionary<string, ServiceConfiguration>();
			//base configuration
			foreach (ServiceElement serviceElement in EdgeServicesConfiguration.Current.Services)
			{
				ServiceConfiguration serviceConfiguration = new ServiceConfiguration();
				serviceConfiguration.Name = serviceElement.Name;

				serviceConfiguration.MaxConcurrent = serviceElement.MaxInstances;
				serviceConfiguration.MaxConcurrentPerProfile = serviceElement.MaxInstancesPerAccount;
				//serviceConfiguration.ID = GetServceConfigruationIDByName(serviceConfiguration.Name);
				baseConfigurations.Add(serviceConfiguration.Name, serviceConfiguration);
			}
			//profiles=account and specific aconfiguration
			foreach (AccountElement account in EdgeServicesConfiguration.Current.Accounts)
			{
				foreach (AccountServiceElement accountService in account.Services)
				{
					ServiceElement serviceUse = accountService.Uses.Element;
					//active element is the calculated configuration 
					ActiveServiceElement activeServiceElement = new ActiveServiceElement(accountService);
					ServiceConfiguration serviceConfiguration = new ServiceConfiguration();
					serviceConfiguration.Name = accountService.Name;
					if (activeServiceElement.Options.ContainsKey("ServicePriority"))
						serviceConfiguration.priority = int.Parse(activeServiceElement.Options["ServicePriority"]);

					serviceConfiguration.MaxConcurrent = (activeServiceElement.MaxInstances == 0) ? 9999 : activeServiceElement.MaxInstances;
					serviceConfiguration.MaxConcurrentPerProfile = (activeServiceElement.MaxInstancesPerAccount == 0) ? 9999 : activeServiceElement.MaxInstancesPerAccount;
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
		}
		/// <summary>
		/// Delete specific instance of service (service for specific time not all the services)
		/// </summary>
		/// <param name="schedulingData"></param>
		public void DeleteScpecificServiceInstance(SchedulingData schedulingData)
		{
			_scheduledServices[schedulingData].Deleted = true;
		}
		/// <summary>
		/// event handler for change of the state of servics
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		void LegacyInstance_StateChanged(object sender, Legacy.ServiceStateChangedEventArgs e)
		{
			if (e.StateAfter == Legacy.ServiceState.Ended)
				_needReschedule = true;
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
			return TimeSpan.FromMinutes(Math.Ceiling(TimeSpan.FromSeconds(averageExacutionTime).TotalMinutes));
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
		/// send event for the services which need to be runing
		/// </summary>
		/// <param name="e"></param>
		public void OnTimeToRun(TimeToRunEventArgs e)
		{
			//foreach (ServiceInstance serviceToRun in e.ServicesToRun)
			//{
			//	Log.Write(this.ToString(), string.Format("Service {0} required to run", serviceToRun.ServiceName), LogMessageType.Information);
			//}
			ServiceRunRequiredEvent(this, e);

		}

		/// <summary>
		/// set event new schedule created
		/// </summary>
		/// <param name="e"></param>
		public void OnNewScheduleCreated(ScheduledInformationEventArgs e)
		{
			NewScheduleCreatedEvent(this, e);
		}

		/// <summary>
		/// abort runing service
		/// </summary>
		/// <param name="schedulingData"></param>
		public void AbortRunningService(SchedulingData schedulingData)
		{
			_scheduledServices[schedulingData].LegacyInstance.Abort();
		}




	}
	public class TimeToRunEventArgs : EventArgs
	{
		public ServiceInstance[] ServicesToRun;
	}
	public class ScheduledInformationEventArgs : EventArgs
	{
		public Dictionary<SchedulingData, ServiceInstance> ScheduleInformation;
		public Dictionary<SchedulingData, ServiceInstance> NotScheduledInformation;
	}
	public class WillNotRunEventArgs : EventArgs
	{
		public List<SchedulingData> WillNotRun = new List<SchedulingData>();
	}
	public class SchedulingInformationEventArgs : EventArgs
	{
		public Dictionary<SchedulingData, ServiceInstanceInfo> ScheduleInformation;
	}




}
