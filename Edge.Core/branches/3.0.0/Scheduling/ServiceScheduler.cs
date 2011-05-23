using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Data.SqlClient;
using Edge.Core.Configuration;
using Edge.Core.Services2.Scheduling;

namespace Edge.Core.Services2.Scheduling
{
	public class ServiceScheduler
	{
		#region Fields
		//======================

		private Dictionary<Guid, ServiceConfiguration> _servicesWarehouse = new Dictionary<Guid,ServiceConfiguration>(); //all services from configuration file load to this var
		private Dictionary<SchedulingInfo, ServiceInstance> _scheduledServices = new Dictionary<SchedulingInfo, ServiceInstance>();
		private Dictionary<int, ServiceConfiguration> _servicesPerConfigurationID = new Dictionary<int, ServiceConfiguration>();
		private Dictionary<int, ServiceConfiguration> _servicesPerProfileID = new Dictionary<int, ServiceConfiguration>();
		private Dictionary<SchedulingInfo, ServiceInstance> _unscheduleServices = new Dictionary<SchedulingInfo, ServiceInstance>();
		//contains average execution time per services per account
		//private Dictionary<string, ServicePerProfileAvgExecutionTimeCache> _servicePerProfileAvgExecutionTimeCash = new Dictionary<string, ServicePerProfileAvgExecutionTimeCache>();
		private Dictionary<ServiceConfiguration, TimeSpan> _executionTimeCach = new Dictionary<ServiceConfiguration, TimeSpan>();
		private DateTime _timeLineFrom;
		private DateTime _timeLineTo;
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
		//TODO :check it with doron- mapping the functions...
		private object _sync;

		public ServiceEnvironment Environment { get; private set; }		
		private DateTime _lastClearCache = DateTime.Now;

		//======================
		#endregion

		#region Events
		//======================

		public event EventHandler ServicesStarted;
		public event EventHandler Rescheduled;

		//======================
		#endregion


		#region Constructors
		//======================

		/// <summary>
		/// Initialize all the services from configuration file or db4o
		/// </summary>
		/// <param name="getServicesFromConfigFile"></param>
		public ServiceScheduler(ServiceEnvironment environment)
		{
			if (environment == null)
				throw new ArgumentNullException("environment");

			this.Environment = environment;

			


			_percentile = 80; //int.Parse(AppSettings.Get(this, "Percentile"));
			_neededScheduleTimeLine = TimeSpan.FromHours(2); //TimeSpan.Parse(AppSettings.Get(this, "NeededScheduleTimeLine"));
			_intervalBetweenNewSchedule = TimeSpan.FromMinutes(10); //TimeSpan.Parse(AppSettings.Get(this, "IntervalBetweenNewSchedule"));
			_findServicesToRunInterval = TimeSpan.FromMinutes(1); //TimeSpan.Parse(AppSettings.Get(this, "FindServicesToRunInterval"));
			_timeToDeleteServiceFromTimeLine = TimeSpan.FromDays(1); //TimeSpan.Parse(AppSettings.Get(this, "DeleteEndedServiceInterval"));
			_executionTimeCacheTimeOutAfter = TimeSpan.FromHours(2); //TimeSpan.Parse(AppSettings.Get(this, "DeleteEndedServiceInterval"));
		}


		//======================
		#endregion


		/// <summary>
		/// The main method of creating schedule 
		/// </summary>
		private void Schedule(bool reschedule)
		{
			List<ServiceInstance> temporarilyCouldNotBeScheduled = new List<ServiceInstance>();
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
					//TODO:TALK TO DORON CAN'T REMOVE FROM IORDERDENURMABLE
					List<ServiceInstance> listOfservicesForNextTimeLine = servicesForNextTimeLine.ToList();
					foreach (ServiceInstance toClear in endedAndTimeToClear)
					{
						_scheduledServices.Remove(toClear.SchedulingInfo); //clear from already schedule table
						//servicesForNextTimeLine.Remove(toClear); //clear from to be scheduled on the curent new schedule
						//TODO:CHECK IT'S REALY CLEAR ???
						listOfservicesForNextTimeLine.Remove(toClear);
						if (toClear.SchedulingInfo.Rule.Scope == SchedulingScope.SingleRun)
							_servicesWarehouse.Remove(toClear.Configuration.ConfigurationID); //clear from services in services wherhouse(all services from configuration and unplaned)

						// Just in case the connection is still up (should never happen)
						((IDisposable)toClear).Dispose();
					}
				}


				//====================================
				#endregion

				// Will include services that could not be fitted into the current schedule, but might be used later


				#region Find Match services
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
															scheduled.Value.SchedulingInfo.EstimateTimeStart ascending
														select
															scheduled;

					//Get all services with same profile
					var servicesWithSameProfile = from scheduled in _scheduledServices
												  where
												  scheduled.Value.Configuration.ByLevel(ServiceConfigurationLevel.Profile) == toBeScheduledInstance.Configuration.ByLevel(ServiceConfigurationLevel.Profile) &&
												  scheduled.Value.State != ServiceState.Ended
												  orderby
												  scheduled.Value.SchedulingInfo.EstimateTimeStart ascending
												  select
												  scheduled;
				#endregion



					#region FindFirstFreeTimeForTheService

					TimeSpan executionTimeInSeconds;
					if (!_executionTimeCach.ContainsKey(toBeScheduledInstance.Configuration.ByLevel(ServiceConfigurationLevel.Profile)))
						executionTimeInSeconds = toBeScheduledInstance.Configuration.ByLevel(ServiceConfigurationLevel.Profile).GetStatistics(_percentile).AverageExecutionTime;
					else
						executionTimeInSeconds = _executionTimeCach[toBeScheduledInstance.Configuration.ByLevel(ServiceConfigurationLevel.Profile)];

					//TimeSpan executionTimeInSeconds = GetAverageExecutionTime(schedulingInfo.Configuration.ServiceName, schedulingInfo.Configuration.Profile.ID, _percentile);

					DateTime baseStartTime = (toBeScheduledInstance.SchedulingInfo.RequestedTimeStart < DateTime.Now) ? DateTime.Now : toBeScheduledInstance.SchedulingInfo.RequestedTimeStart;
					DateTime baseEndTime = baseStartTime.Add(executionTimeInSeconds);
					DateTime calculatedStartTime = baseStartTime;
					DateTime calculatedEndTime = baseEndTime;
					bool found = false;


					while (!found)
					{
						int countedPerConfiguration = servicesWithSameConfiguration.Count(scheduled => (calculatedStartTime >= scheduled.Value.SchedulingInfo.EstimateTimeStart && calculatedStartTime <= scheduled.Value.SchedulingInfo.EstimateTimeEnd) || (calculatedEndTime >= scheduled.Value.SchedulingInfo.EstimateTimeStart && calculatedEndTime <= scheduled.Value.SchedulingInfo.EstimateTimeEnd));
						if (countedPerConfiguration < toBeScheduledInstance.Configuration.ByLevel(ServiceConfigurationLevel.Profile).Limits.MaxConcurrentGlobal)
						{
							int countedPerProfile = servicesWithSameProfile.Count(scheduled => (calculatedStartTime >= scheduled.Value.SchedulingInfo.EstimateTimeStart && calculatedStartTime <= scheduled.Value.SchedulingInfo.EstimateTimeEnd) || (calculatedEndTime >= scheduled.Value.SchedulingInfo.EstimateTimeStart && calculatedEndTime <= scheduled.Value.SchedulingInfo.EstimateTimeEnd));
							if (countedPerProfile < toBeScheduledInstance.Configuration.ByLevel(ServiceConfigurationLevel.Profile).Limits.MaxConcurrentPerProfile)
							{

								toBeScheduledInstance.SchedulingInfo.EstimateTimeStart = calculatedStartTime;
								toBeScheduledInstance.SchedulingInfo.EstimateTimeEnd = calculatedEndTime;
								toBeScheduledInstance.SchedulingInfo.EstimatePercentile = _percentile; //TODO: TALK TO DORON WHERE THE ODDS BELONG?
								toBeScheduledInstance.SchedulingInfo.ActualDeviation = calculatedStartTime.Subtract(toBeScheduledInstance.SchedulingInfo.RequestedTimeStart);
								//toBeScheduledInstance.StateChanged += new EventHandler(LegacyInstance_StateChanged);
								toBeScheduledInstance.StateChanged += new EventHandler(toBeScheduledInstance_StateChanged);
								found = true;
							}
						}
						if (!found)
						{

							//GetNewStartEndTime
							calculatedStartTime = servicesWithSameProfile.Where(s => s.Value.SchedulingInfo.EstimateTimeEnd >= calculatedStartTime).Min(s => s.Value.SchedulingInfo.EstimateTimeEnd);
							if (calculatedStartTime < DateTime.Now)
								calculatedStartTime = DateTime.Now;

							calculatedEndTime = calculatedStartTime + (executionTimeInSeconds);

							////remove unfree time from servicePerConfiguration and servicePerProfile
							servicesWithSameConfiguration = from s in servicesWithSameConfiguration
															where s.Value.SchedulingInfo.EstimateTimeEnd > calculatedStartTime
															orderby s.Value.SchedulingInfo.EstimateTimeStart
															select s;

							servicesWithSameProfile = from s in servicesWithSameProfile
													  where s.Value.SchedulingInfo.EstimateTimeEnd > calculatedStartTime
													  orderby s.Value.SchedulingInfo.EstimateTimeStart
													  select s;
						}
					}

					#endregion


					#region Add the service to schedule table and notify temporarly unschedlued
					if (toBeScheduledInstance.SchedulingInfo.ActualDeviation > toBeScheduledInstance.SchedulingInfo.Rule.MaxDeviationAfter && toBeScheduledInstance.SchedulingInfo.Rule.Scope != SchedulingScope.SingleRun)
						temporarilyCouldNotBeScheduled.Add(toBeScheduledInstance);
					else
						_scheduledServices.Add(toBeScheduledInstance.SchedulingInfo, toBeScheduledInstance);
					#endregion


				}
			}

			if (Rescheduled != null)
				Rescheduled(this, new EventArgs());

		}

		

		void toBeScheduledInstance_StateChanged(object sender, EventArgs e)
		{

			ServiceInstance instance = (ServiceInstance)sender;
			if (instance.State == ServiceState.Ended)
				_needReschedule = true;
		}

		/// <summary>
		/// add unplanned service to schedule
		/// </summary>
		/// <param name="serviceConfiguration"></param>
		public void AddServiceToSchedule(ServiceConfiguration serviceConfiguration)
		{
			lock (_servicesWarehouse)
			{

				_servicesWarehouse.Add(serviceConfiguration.ConfigurationID, serviceConfiguration);
			}
			_needReschedule = true;
		}
		public void RemoveFromSchedule(Guid serviceConfigurationID)
		{
			lock (_servicesWarehouse)
			{
				if (!_servicesWarehouse.ContainsKey(serviceConfigurationID))
					throw new Exception(String.Format("Service wharehouse does not contains service configuration: {0}", serviceConfigurationID));

				_servicesWarehouse.Remove(serviceConfigurationID);
				
			}

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
				foreach (ServiceConfiguration serviceConfig in _servicesWarehouse.Values)
				{
					foreach (SchedulingRule schedulingRule in serviceConfig.SchedulingRules)
					{
						
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
									case SchedulingScope.SingleRun:
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
					if (_lastClearCache.Add(_executionTimeCacheTimeOutAfter) < DateTime.Now)
						_executionTimeCach.Clear();
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
				foreach (var scheduleService in _scheduledServices.OrderBy(schedule => schedule.Value.SchedulingInfo.EstimateTimeStart))
				{
					if (scheduleService.Value.SchedulingInfo.EstimateTimeStart.Day == DateTime.Now.Day) //same day
					{
						// find unitialized services scheduled since the last interval
						//if (scheduleService.Value.StartTime > DateTime.Now - FindServicesToRunInterval-FindServicesToRunInterval &&
						//    scheduleService.Value.StartTime <= DateTime.Now &&
						//    scheduleService.Value.LegacyInstance.State == Legacy.ServiceState.Uninitialized)
						if (scheduleService.Value.SchedulingInfo.EstimateTimeStart <= DateTime.Now &&
							scheduleService.Value.SchedulingInfo.RequestedTimeStart.Add(scheduleService.Key.Rule.MaxDeviationAfter) >= DateTime.Now &&
						   scheduleService.Value.State == ServiceState.Uninitialized)
						{
							instancesToRun.Add(scheduleService.Value);
						}
					}
				}
			}

			if (instancesToRun.Count > 0)
			{
				instancesToRun = (List<ServiceInstance>)instancesToRun.OrderBy(s => s.SchedulingInfo.EstimateTimeStart).ToList<ServiceInstance>();
				ServicesStarted(this, new TimeToRunEventArgs() { ServicesToRun = instancesToRun.ToArray() });
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
