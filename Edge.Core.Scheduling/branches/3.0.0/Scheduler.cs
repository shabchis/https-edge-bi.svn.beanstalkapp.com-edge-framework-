using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Edge.Core.Utilities;
using Edge.Core.Scheduling;
using System.ServiceModel;
using System.Diagnostics;

namespace Edge.Core.Services.Scheduling
{
	[ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
	public class Scheduler
	{
		#region Data Members

		private object _instanceLock = new object();

		public ServiceEnvironment Environment { get; private set; }
		public SchedulerConfiguration Configuration { get; private set; }
		
		private Dictionary<string, ServiceConfiguration> _serviceBaseConfigurations = new Dictionary<string, ServiceConfiguration>();

		// Configurations from config or from unplanned - 'Schedule' method goes over this to find things that need scheduling
		private List<ServiceConfiguration> _serviceConfigurationsToSchedule = new List<ServiceConfiguration>();

		// Unscheduled requests waiting to be scheduled
		private InstanceRequestCollection _unscheduledRequests = new InstanceRequestCollection();

		// Scheduled instances that are added by the 'Schedule' method
		private InstanceRequestCollection _scheduledRequests = new InstanceRequestCollection();

		// Dictionary of execution statistics times per service and profile config
		// according to the table in DB which is updated every X time and reloaded into dictionary every X time by configuration
		private Dictionary<string, long> _servicesExecutionStatisticsDict = new Dictionary<string, long>();
		
		DateTime _timeframeFrom;
		DateTime _timeframeTo;
		
		private volatile bool _needReschedule;
		private bool _started;
		
		Action _schedulerTimer;
		Action _executeServicesTimer;
		Action _executionStatisticsRefreshTimer;
		
		#endregion

		#region Properties
		public InstanceRequestCollection ScheduledServices
		{
			get { return _scheduledRequests; }
		}

		public InstanceRequestCollection UnscheduledServices
		{
			get { return _unscheduledRequests; }
		}

		public List<ServiceConfiguration> ServiceConfigurations
		{
			get { return _serviceConfigurationsToSchedule; }
		}

		public ProfilesCollection Profiles
		{
			get { return Configuration.Profiles; }
		}

		public Dictionary<string, long> ServicesExecutionStatisticsDict
		{
			get { return _servicesExecutionStatisticsDict; }
		}

		#endregion

		#region Ctor
		public Scheduler() {}

		public Scheduler(ServiceEnvironment environment, SchedulerConfiguration configuration)
		{
			Configuration = configuration;
			
			// set environment and register to env event for scheduling services (from workflow)
			Environment = environment;
			environment.ListenForEvents(ServiceEnvironmentEventType.ServiceScheduleRequested);
			environment.ServiceScheduleRequested += environment_ServiceScheduleRequested;

			// init base service dictionary
			foreach (var serviceConfig in Configuration.ServiceConfigurationList)
			{
				_serviceBaseConfigurations.Add(serviceConfig.ServiceName, serviceConfig);
			}

			// add services to schedule
			foreach (var service in Configuration.Profiles.SelectMany(profile => profile.Services))
			{
				_serviceConfigurationsToSchedule.Add(service);
			}

			LoadServiceExecutionStatistics();

			LoadRecovery();

			DebugStartupInfo();
		}

		#endregion
		
		#region Public Methods
		
		/// <summary>
		/// start the timers of new scheduling and services required to execute
		/// </summary>
		public void Start()
		{
			WriteLog("Init: Starting scheduler");
			if (_started)
			{
				return;
			}
			_started = true;

			// scheduler timer
			_schedulerTimer = SchedulerTimer;
			_schedulerTimer.BeginInvoke(result =>
			{
				try
				{
					_schedulerTimer.EndInvoke(result);
				}
				catch (Exception ex)
				{
					Log.Write(this.ToString(), ex.Message, ex, LogMessageType.Error);
				}
			}, null);

			// execute services timer
			_executeServicesTimer = ExecuteServicesTimer;
			_executeServicesTimer.BeginInvoke(result =>
			{
				try
				{
					_executeServicesTimer.EndInvoke(result);
				}
				catch (Exception ex)
				{

					Log.Write(this.ToString(), ex.Message, ex, LogMessageType.Error);
				}
			}, null);

			// refresh execution statistics timer
			_executionStatisticsRefreshTimer = RefreshExecutionStatisticsTimer;
			_executionStatisticsRefreshTimer.BeginInvoke(result =>
			{
				try
				{
					_executionStatisticsRefreshTimer.EndInvoke(result);
				}
				catch (Exception ex)
				{

					Log.Write(this.ToString(), ex.Message, ex, LogMessageType.Error);
				}
			}, null);
		}

		/// <summary>
		///  stop the timers of new scheduling and services required to execute
		/// </summary>
		public void Stop()
		{
			WriteLog("Stop scheduler");
			_started = false;
		}
		#endregion

		#region Timer Methods
		private void SchedulerTimer()
		{
			WriteLog("Init: StartSchedulerTimer");
			Schedule();
			TimeSpan calcTimeInterval = Configuration.SamplingInterval;

			while (_started)
			{
				Thread.Sleep(TimeSpan.FromSeconds(2));

				if (_needReschedule || calcTimeInterval <= TimeSpan.Zero)
				{
					Schedule();
					calcTimeInterval = Configuration.SamplingInterval;
				}
				else
				{
					calcTimeInterval = calcTimeInterval.Subtract(TimeSpan.FromSeconds(2));
				}
			}
		}

		private void ExecuteServicesTimer()
		{
			WriteLog("Init: StartRequiredServicesTimer");
			while (_started)
			{
				Thread.Sleep(Configuration.ResheduleInterval);
				ExecuteScheduledRequests();
			}
		}
		
		private void RefreshExecutionStatisticsTimer()
		{
			WriteLog("Init: RefreshExecutionStatisticsTimer");
			while (_started)
			{
				Thread.Sleep(Configuration.ExecutionStatisticsRefreshInterval);
				LoadServiceExecutionStatistics();
			}
		}
		
		#endregion

		#region Scheduling algorithms
		
		/// <summary>
		/// The main method of scheduler calculation
		/// </summary>
		public void Schedule(bool reschedule = false)
		{
			WriteLog(String.Format("Start scheduling: scheduled={0} (uninitialized={1}, ended={2}), unscheduled={3}",
								_scheduledRequests.Count, _scheduledRequests.Count(s => s.State == ServiceState.Uninitialized),
								_scheduledRequests.Count(s => s.State == ServiceState.Ended), _unscheduledRequests.Count)); 
			
			lock (_unscheduledRequests)
			{
				lock (_scheduledRequests)
				{
					// Set need reschedule to false in order to avoid more schedule from other threads
					_needReschedule = false;

					#region Manage history and find services to schedule
					// ------------------------------------

					// first of all remove old not relevant scheduled request
					_scheduledRequests.RemoveEndedRequests();

					// Move pending uninitialized services to the unscheduled list so they can be rescheduled
                    foreach (ServiceInstance request in _scheduledRequests.RemoveNotActivated())
					{
						if (request.SchedulingInfo.RequestedTime + request.SchedulingInfo.MaxDeviationAfter > DateTime.Now)
						{
							WriteLog(String.Format("Move scheduled request to unscheduled list '{0}'", InstanceRequestCollection.GetSignature(request)));
							_unscheduledRequests.Add(request);
						}
						else
						{
							AsLockable(request).Unlock(_instanceLock);
							WriteLog(String.Format("Request request '{0}'can not be scheduled", InstanceRequestCollection.GetSignature(request)));
							request.SchedulingInfo.SchedulingStatus = SchedulingStatus.CouldNotBeScheduled;
							AsLockable(request).Lock(_instanceLock);
						}
					}

					// Get Services for next timeframe
					foreach (ServiceInstance request in GetServicesInTimeframe(reschedule))
					{
						WriteLog(String.Format("Add request to unscheduled list '{0}'", InstanceRequestCollection.GetSignature(request)));
						_unscheduledRequests.Add(request);
					}

					// Copy unscheduled requests to an ordered list by request time + max deviation after
					var servicesForNextTimeframe = new List<ServiceInstance>(_unscheduledRequests
													.OrderBy(schedulingData => schedulingData.SchedulingInfo.RequestedTime + schedulingData.SchedulingInfo.MaxDeviationAfter));

					// ------------------------------------
					#endregion

					#region Find Match services
					// ------------------------------------

					//Same services or same services with same profile
					foreach (ServiceInstance serviceInstance in servicesForNextTimeframe)
					{
						//Get all services with same configurationID
						var requestsWithSameConfiguration = _scheduledRequests.GetWithSameConfiguration(serviceInstance);

						//Get all services with same profileID
						var requestsWithSameProfile = _scheduledRequests.GetWithSameProfile(serviceInstance);

						//take execution time per service and profile (if profile does not exist try to take per service only)
						TimeSpan avgExecutionTime = GetExecutionStatisticsForService(serviceInstance.Configuration.ServiceName,
													serviceInstance.Configuration.Profile == null ? String.Empty : 
													serviceInstance.Configuration.Profile.Parameters["AccountID"].ToString());

						DateTime baseStartTime = (serviceInstance.SchedulingInfo.RequestedTime < DateTime.Now) ? DateTime.Now : serviceInstance.SchedulingInfo.RequestedTime;
						DateTime baseEndTime = baseStartTime.Add(avgExecutionTime);
						DateTime calculatedStartTime = baseStartTime;
						DateTime calculatedEndTime = baseEndTime;

						bool found = false;
						while (!found)
						{
							IOrderedEnumerable<ServiceInstance> whereToLookNext = null;

							int countedPerConfiguration = requestsWithSameConfiguration.Count(s => (calculatedStartTime >= s.SchedulingInfo.ExpectedStartTime && calculatedStartTime <= s.SchedulingInfo.ExpectedEndTime) || (calculatedEndTime >= s.SchedulingInfo.ExpectedStartTime && calculatedEndTime <= s.SchedulingInfo.ExpectedEndTime));
							if (countedPerConfiguration < serviceInstance.Configuration.Limits.MaxConcurrentPerTemplate)
							{
								int countedPerProfile = requestsWithSameProfile.Count(s => (calculatedStartTime >= s.SchedulingInfo.ExpectedStartTime && calculatedStartTime <= s.SchedulingInfo.ExpectedEndTime) || (calculatedEndTime >= s.SchedulingInfo.ExpectedStartTime && calculatedEndTime <= s.SchedulingInfo.ExpectedEndTime));
								if (countedPerProfile < serviceInstance.Configuration.Limits.MaxConcurrentPerProfile)
								{
									AsLockable(serviceInstance).Unlock(_instanceLock);
									serviceInstance.SchedulingInfo.ExpectedStartTime = calculatedStartTime;
									serviceInstance.SchedulingInfo.ExpectedEndTime = calculatedEndTime;
									serviceInstance.SchedulingInfo.SchedulingStatus = SchedulingStatus.Scheduled;
									AsLockable(serviceInstance).Lock(_instanceLock);

									WriteLog(String.Format("Schedule service '{0}'. Expected start time={1}, expected end time={2}",
													InstanceRequestCollection.GetSignature(serviceInstance), 
													serviceInstance.SchedulingInfo.ExpectedStartTime, 
													serviceInstance.SchedulingInfo.ExpectedEndTime));
									found = true;
								}
								else
								{
									whereToLookNext = requestsWithSameProfile;
								}
							}
							else
							{
								whereToLookNext = requestsWithSameConfiguration;
							}

							if (!found)
							{
								if (whereToLookNext == null)
									throw new Exception("This should not have happened.");

								calculatedStartTime = whereToLookNext.Count() > 0 ? 
													  whereToLookNext.Where(s => s.SchedulingInfo.ExpectedEndTime >= calculatedStartTime).Min(s => s.SchedulingInfo.ExpectedEndTime) :
													  calculatedStartTime;

								if (calculatedStartTime < DateTime.Now)
									calculatedStartTime = DateTime.Now;

								//Get end time
								calculatedEndTime = calculatedStartTime.Add(avgExecutionTime);

								//remove unfree time from servicePerConfiguration and servicePerProfile							
								if (calculatedStartTime <= _timeframeTo)
								{
									requestsWithSameConfiguration = from s in requestsWithSameConfiguration
																	where s.SchedulingInfo.ExpectedEndTime > calculatedStartTime
																	orderby s.SchedulingInfo.ExpectedStartTime
																	select s;

									requestsWithSameProfile = from s in requestsWithSameProfile
															  where s.SchedulingInfo.ExpectedEndTime > calculatedStartTime
															  orderby s.SchedulingInfo.ExpectedStartTime
															  select s;
								}
							}
						}

						if (serviceInstance.SchedulingInfo.ExpectedDeviation <= serviceInstance.SchedulingInfo.MaxDeviationAfter || serviceInstance.SchedulingInfo.MaxDeviationAfter == TimeSpan.Zero)
						{
							AsLockable(serviceInstance).Unlock(_instanceLock);
							serviceInstance.SchedulingInfo.SchedulingStatus = SchedulingStatus.Scheduled;
							serviceInstance.StateChanged += Instance_StateChanged;
							AsLockable(serviceInstance).Lock(_instanceLock);

							// set service instance max execution time
							TimeSpan maxExecutionTime = TimeSpan.FromMilliseconds(avgExecutionTime.TotalMilliseconds * Configuration.MaxExecutionTimeFactor);
							serviceInstance.Configuration.Limits.MaxExecutionTime = maxExecutionTime;

							if (!_scheduledRequests.ContainsSignature(serviceInstance))
							{
								WriteLog(String.Format("Move unscheduled request to scheduled list '{0}'", InstanceRequestCollection.GetSignature(serviceInstance)));
								_scheduledRequests.Add(serviceInstance);
								_unscheduledRequests.Remove(serviceInstance);
							}
							else
							{
								WriteLog(String.Format("Warning! Request '{0}' already exists in scheduled list", InstanceRequestCollection.GetSignature(serviceInstance)));
							}
						}
					}
					#endregion
				}
				WriteLog(String.Format("Finish scheduling: scheduled={0} (uninitialized={1}, ended={2}), unscheduled={3}", 
								_scheduledRequests.Count, _scheduledRequests.Count(s=> s.State == ServiceState.Uninitialized), 
								_scheduledRequests.Count(s=> s.State == ServiceState.Ended), _unscheduledRequests.Count));
				ExecuteScheduledRequests();
			}
		}

		/// <summary>
		/// Get this time line services 
		/// </summary>
		/// <param name="useCurrentTimeframe">if it's for reschedule then the time line is the same as the last schedule</param>
		/// <returns></returns>
		private IEnumerable<ServiceInstance> GetServicesInTimeframe(bool useCurrentTimeframe)
		{
			// Take next timeframe if false
			if (!useCurrentTimeframe)
			{
				_timeframeFrom = DateTime.Now;
				_timeframeTo = DateTime.Now.Add(Configuration.Timeframe);
				WriteLog(String.Format("Current timeframe {0} - {1}", _timeframeFrom, _timeframeTo));
			}

			lock (_serviceConfigurationsToSchedule)
			{
				for (int i = 0; i < _serviceConfigurationsToSchedule.Count; i++)
				{
					ServiceConfiguration configuration = _serviceConfigurationsToSchedule[i];

					foreach (SchedulingRule schedulingRule in configuration.SchedulingRules)
					{
						foreach (TimeSpan time in schedulingRule.Times)
						{
							DateTime requestedTime = (_timeframeFrom.Date + time);

							while (requestedTime.Date <= _timeframeTo.Date)
							{
								if (IsRuleInTimeframe(schedulingRule, requestedTime))
								{
									// create service instance, check if it already exists and return it
									ServiceInstance request = CreateServiceInstance(configuration, schedulingRule, requestedTime);
									lock (_scheduledRequests)
									{
										lock (_unscheduledRequests)
										{
											if (!_unscheduledRequests.ContainsSignature(request) && !_scheduledRequests.ContainsSignature(request))
												yield return request;
										}
									}
								}
								requestedTime = requestedTime.AddDays(1);
							}
						}
					}
				}
			}
		}
		
		private bool IsRuleInTimeframe(SchedulingRule schedulingRule, DateTime requestedTime)
		{
			bool isRuleInTimeframe = false;

			// check if in timeframe by scope
			switch (schedulingRule.Scope)
			{
				case SchedulingScope.Day:
					isRuleInTimeframe = true;
					break;
				case SchedulingScope.Week:
					var dayOfWeek = (int)requestedTime.DayOfWeek;
					if (schedulingRule.Days.Contains(dayOfWeek))
						isRuleInTimeframe = true;
					break;
				case SchedulingScope.Month:
					var dayOfMonth = requestedTime.Day;
					if (schedulingRule.Days.Contains(dayOfMonth))
						isRuleInTimeframe = true;
					break;
			}

			isRuleInTimeframe = (isRuleInTimeframe) &&
								(requestedTime >= _timeframeFrom && requestedTime <= _timeframeTo) ||
								(requestedTime <= _timeframeFrom && 
								(schedulingRule.MaxDeviationAfter == TimeSpan.Zero || 
								 requestedTime.Add(schedulingRule.MaxDeviationAfter) >= DateTime.Now));

			return isRuleInTimeframe;
		}

		private ServiceInstance CreateServiceInstance(ServiceConfiguration config, SchedulingRule rule, DateTime requestedTime)
		{
			ServiceInstance request = Environment.NewServiceInstance(config);
			
			request.SchedulingInfo = new SchedulingInfo
				{
				SchedulingStatus = SchedulingStatus.New,
				SchedulingScope = rule.Scope,
				RequestedTime = requestedTime,
				MaxDeviationBefore = rule.MaxDeviationBefore,
				MaxDeviationAfter = rule.MaxDeviationAfter,
			};
			AsLockable(request).Lock(_instanceLock);

			return request;
		}
		
		/// <summary>
		/// Start scheduled service instances if in time
		/// </summary>
		private void ExecuteScheduledRequests()
		{
			//WriteLog("ExecuteScheduledRequests");
			lock (_scheduledRequests)
			{
				foreach (var request in _scheduledRequests.OrderBy(s => s.SchedulingInfo.ExpectedStartTime))
				{
					// check if it is time to execute request
					if ( request.SchedulingInfo.ExpectedStartTime <= DateTime.Now &&
						(request.SchedulingInfo.MaxDeviationAfter == TimeSpan.Zero || request.SchedulingInfo.RequestedTime.Add(request.SchedulingInfo.MaxDeviationAfter) >= DateTime.Now) &&
						 request.SchedulingInfo.SchedulingStatus == SchedulingStatus.Scheduled)
					{
						// additional check for concurent services per template and profile wchich are scheduled for now or past
						int countedServicesWithSameConfiguration = _scheduledRequests.GetWithSameConfiguration(request).Count(s => s.SchedulingInfo.ExpectedStartTime <= DateTime.Now);
						if (countedServicesWithSameConfiguration >= request.Configuration.Limits.MaxConcurrentPerTemplate)
							continue;

						int countedServicesWithSameProfile = _scheduledRequests.GetWithSameProfile(request).Count(s => s.SchedulingInfo.ExpectedStartTime <= DateTime.Now);
						if (countedServicesWithSameProfile >= request.Configuration.Limits.MaxConcurrentPerProfile)
							continue;

						// start service instance
						WriteLog(String.Format("Start service '{0}'", InstanceRequestCollection.GetSignature(request)));
						request.Start();
					}
				}
			}
		}

		/// <summary>
		/// Load execution statistics per service/profile from environment
		/// </summary>
		private void LoadServiceExecutionStatistics()
		{
			if (Environment != null)
			{
				_servicesExecutionStatisticsDict = Environment.GetServiceExecutionStatistics(Configuration.Percentile);
				WriteLog(String.Format("Loaded execution statistics for percentile {0}: {1} records loaded", Configuration.Percentile, ServicesExecutionStatisticsDict.Count));
			}
		}

		/// <summary>
		/// Get execution statistics per specific service and profile, default is 180 sec
		/// </summary>
		/// <param name="serviceConfigID"></param>
		/// <param name="profileID"></param>
		/// <returns></returns>
		private TimeSpan GetExecutionStatisticsForService(string serviceConfigID, string profileID)
		{
			long statisticsTime = 60;
			var key = String.Format("ConfigID:{0},ProfileID:{1}", serviceConfigID, profileID);

			if (_servicesExecutionStatisticsDict.ContainsKey(key))
			{
				statisticsTime = _servicesExecutionStatisticsDict[key];
			}
			return TimeSpan.FromSeconds(statisticsTime);
		}

		#endregion

		#region Private Methods

		/// <summary>
		/// In startup load services which are already executed from DB for recovery purpose
		/// in order not to execute already executed services
		/// </summary>
		private void LoadRecovery()
		{
			if (Environment != null)
			{
				var instanceList = Environment.GetServiceInstanceActiveList();
				foreach (var instance in instanceList)
				{
					_scheduledRequests.Add(instance);
				}
			}
		}

		private void AddRequestToSchedule(ServiceInstance request)
		{
			// add scheduling info if not exists
			if (request.SchedulingInfo == null)
			{
				AsLockable(request).Unlock(_instanceLock);
				request.SchedulingInfo = new SchedulingInfo
					{
						SchedulingStatus = SchedulingStatus.New,
						RequestedTime = DateTime.Now,
						SchedulingScope = SchedulingScope.Unplanned
					};
				AsLockable(request).Lock(_instanceLock);
			}

			WriteLog(String.Format("Add request to unscheduled list '{0}'", InstanceRequestCollection.GetSignature(request)));
			lock (_unscheduledRequests)
			{
				_unscheduledRequests.Add(request);
			}
			_needReschedule = true;
		}

		private void AddUnplannedServiceToSchedule(ServiceInstance instance)
		{
			ServiceConfiguration baseConfiguration;
			if (!_serviceBaseConfigurations.TryGetValue(instance.Configuration.ServiceName, out baseConfiguration))
				throw new KeyNotFoundException(String.Format("No base configuration exists for the service '{0}'.", instance.Configuration.ServiceName));

			ServiceProfile profile;
			if (!Configuration.Profiles.TryGetValue(Convert.ToInt32(instance.Configuration.Profile.Parameters["AccountID"]), out profile))
				throw new KeyNotFoundException(String.Format("No profile exists with the ID '{0}' (account ID).", (instance.Configuration.Profile.Parameters["AccountID"])));

			if (instance.Configuration.SchedulingRules[0].Scope != SchedulingScope.Unplanned)
				throw new Exception("instance rule is not unnplaned, scheduler only get services");

			AddRequestToSchedule(instance);
		}

		private static ILockable AsLockable(ILockable obj)
		{
			return obj;
		}

		private void DebugStartupInfo()
		{
			foreach (var service in _serviceConfigurationsToSchedule)
			{
				var profile = service.GetProfileConfiguration() == null ? null : service.GetProfileConfiguration().Profile;
				foreach (var rule in service.SchedulingRules)
				{
					WriteLog(String.Format("Init: Service {0}, profile {1}, rule: scope={2}, time={3}, day={4}, max deviation after={5}, max deviation before={6}",
									service.ServiceName, profile != null ? profile.Name : String.Empty, rule.Scope, rule.Times[0], rule.Days[0],
									rule.MaxDeviationAfter, rule.MaxDeviationBefore));

				}
			}

			WriteLog(String.Format("Init: Service instances from recovery count={0}", _scheduledRequests.Count));
			
		}

		private void WriteLog(string message)
		{
			Debug.WriteLine("{0}: {1}", DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"), message);
		}
		#endregion

		#region Events
		/// <summary>
		/// Add event from the environment to schedule service
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void environment_ServiceScheduleRequested(object sender, ServiceScheduleRequestedEventArgs e)
		{
			if (e.ServiceInstance != null)
			{
				if (e.ServiceInstance.ParentInstance == null)
				{
					AddUnplannedServiceToSchedule(e.ServiceInstance);
				}
				else
				{
					AddRequestToSchedule(e.ServiceInstance);
				}
			}
		}

		private void Instance_StateChanged(object sender, EventArgs e)
		{
			var instance = (ServiceInstance)sender;
			AsLockable(instance).Unlock(_instanceLock);
			instance.SchedulingInfo.SchedulingStatus = SchedulingStatus.Activated;
			AsLockable(instance).Lock(_instanceLock);

			WriteLog(String.Format("Service '{0}' is {1}", InstanceRequestCollection.GetSignature(instance), instance.State.ToString()));
		}
		#endregion
	}

	#region Extensions
	public static class DateTimeExtenstions
	{
		public static DateTime RemoveSeconds(this DateTime time)
		{
			return new DateTime(time.Year, time.Month, time.Day, time.Hour, time.Minute, 0, 0);
		}

		public static DateTime RemoveMilliseconds(this DateTime time)
		{
			return new DateTime(time.Year, time.Month, time.Day, time.Hour, time.Minute, time.Second, 0);
		}
	}
	#endregion
}
