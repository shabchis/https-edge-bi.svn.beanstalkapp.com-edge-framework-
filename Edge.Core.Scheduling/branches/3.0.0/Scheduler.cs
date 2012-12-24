using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Edge.Core.Utilities;
using System.ServiceModel;
using System.Diagnostics;

namespace Edge.Core.Services.Scheduling
{
	[ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
	public class Scheduler : IDisposable
	{
		#region Data Members

		private object _instanceLock = new object();

		public ServiceEnvironment Environment { get; private set; }
		private readonly ServiceEnvironmentEventListener _listener;
		public SchedulerConfiguration Configuration { get; private set; }
		
		private Dictionary<string, ServiceConfiguration> _serviceBaseConfigurations = new Dictionary<string, ServiceConfiguration>();

		// Configurations from config or from unplanned - 'Schedule' method goes over this to find things that need scheduling
		private List<ServiceConfiguration> _serviceConfigurationsToSchedule = new List<ServiceConfiguration>();

		// Unscheduled requests waiting to be scheduled
		private InstanceRequestCollection _unscheduledRequests = new InstanceRequestCollection {CollectionType = "Unscheduled"};

		// Scheduled instances that are added by the 'Schedule' method
		private InstanceRequestCollection _scheduledRequests = new InstanceRequestCollection {CollectionType = "Scheduled"};

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
			_listener = environment.ListenForEvents(ServiceEnvironmentEventType.ServiceRequiresScheduling);
			_listener.ServiceRequiresScheduling += Listener_ServiceRequiresScheduling;
			
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
					Log.Write(ToString(), ex.Message, ex);
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
					Log.Write(ToString(), ex.Message, ex);
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
					Log.Write(ToString(), ex.Message, ex);
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
			TimeSpan calcTimeInterval = Configuration.RescheduleInterval;

			while (_started)
			{
				Thread.Sleep(Configuration.CheckUnplannedServicesInterval);

				if (_needReschedule || calcTimeInterval <= TimeSpan.Zero)
				{
					Schedule();
					calcTimeInterval = Configuration.RescheduleInterval;
				}
				else
				{
					calcTimeInterval = calcTimeInterval.Subtract(Configuration.CheckUnplannedServicesInterval);
				}
			}
		}

		private void ExecuteServicesTimer()
		{
			WriteLog("Init: StartRequiredServicesTimer");
			while (_started)
			{
				Thread.Sleep(Configuration.ExecuteInterval);
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
			try
			{
				lock (_unscheduledRequests)
				{
					lock (_scheduledRequests)
					{
						// Set need reschedule to false in order to avoid more schedule from other threads
						_needReschedule = false;

						#region Manage history and find services to schedule
						// ------------------------------------

						RemoveNotRelevantRequests();

						// Move pending uninitialized services to the unscheduled list so they can be rescheduled
						foreach (ServiceInstance request in _scheduledRequests.RemoveNotActivated())
						{
							if (request.SchedulingInfo.RequestedTime + request.SchedulingInfo.MaxDeviationAfter > DateTime.Now)
							{
								WriteLog(String.Format("Move scheduled request to unscheduled list '{0}'", request.DebugInfo()));
								_unscheduledRequests.Add(request);
							}
							else
							{
								// mark service as could not be scheduled because of deviation
								ServiceCannotBeScheduled(request);
							}
						}

						// Get Services for next timeframe
						foreach (ServiceInstance request in GetServicesInTimeframe(reschedule))
						{
							WriteLog(String.Format("Add request to unscheduled list '{0}'", request.DebugInfo()));
							_unscheduledRequests.Add(request);
						}

						// Copy unscheduled requests to an ordered list by request time + max deviation after
						var servicesForNextTimeframe = new List<ServiceInstance>(_unscheduledRequests.OrderBy(
								                                                         schedulingData =>
								                                                         schedulingData.SchedulingInfo.RequestedTime +
								                                                         schedulingData.SchedulingInfo.MaxDeviationAfter));
						// ------------------------------------
						#endregion

						#region Find Match services
						// ------------------------------------

						//Same services or same services with same profile
						foreach (ServiceInstance serviceInstance in servicesForNextTimeframe)
						{
							//Get all services with same configurationID (concurrent per Template)
							var requestsWithSameTemplate = _scheduledRequests.GetWithSameTemplate(serviceInstance);

							//Get all services with same profileID (concurrent per Profile)
							var requestsWithSameProfile = _scheduledRequests.GetWithSameProfile(serviceInstance);

							//take execution time by service configuration
							TimeSpan avgExecutionTime = GetExecutionStatisticsForService(serviceInstance.Configuration);

							DateTime baseStartTime = serviceInstance.SchedulingInfo.RequestedTime < DateTime.Now ? DateTime.Now : serviceInstance.SchedulingInfo.RequestedTime;
							DateTime baseEndTime = baseStartTime.Add(avgExecutionTime);
							DateTime calculatedStartTime = baseStartTime;
							DateTime calculatedEndTime = baseEndTime;

							while (true)
							{
								IOrderedEnumerable<ServiceInstance> whereToLookNext;

								// check concurren per Template
								var countPerTemplate = CountConcurrent(requestsWithSameTemplate, calculatedStartTime, calculatedEndTime);
								if (countPerTemplate < serviceInstance.Configuration.Limits.MaxConcurrentPerTemplate)
								{
									// check concurrent per Profile
									var countPerProfile = CountConcurrent(requestsWithSameProfile, calculatedStartTime, calculatedEndTime);
									if (countPerProfile < serviceInstance.Configuration.Limits.MaxConcurrentPerProfile)
									{
										// if no concurrents set scheduling time (found time for scheduling)
										AsLockable(serviceInstance).Unlock(_instanceLock);
										serviceInstance.SchedulingInfo.ExpectedStartTime = calculatedStartTime;
										serviceInstance.SchedulingInfo.ExpectedEndTime = calculatedEndTime;
										AsLockable(serviceInstance).Lock(_instanceLock);
										break;
									}
									whereToLookNext = requestsWithSameProfile;
								}
								else
								{
									whereToLookNext = requestsWithSameTemplate;
								}

								if (whereToLookNext == null) throw new Exception("This should not have happened.");

								// try to take the next available time to schedule the service (end time of concurrent service)
								calculatedStartTime = !whereToLookNext.Any() ? calculatedStartTime :
													   whereToLookNext.Where(s => s.SchedulingInfo.ExpectedEndTime >= calculatedStartTime).Min(s => s.SchedulingInfo.ExpectedEndTime);

								if (calculatedStartTime < DateTime.Now) calculatedStartTime = DateTime.Now;
								calculatedEndTime = calculatedStartTime.Add(avgExecutionTime);

								// check if a new calculated time is still valid according to max deviation after					
								if (calculatedStartTime <= serviceInstance.SchedulingInfo.RequestedTime + serviceInstance.SchedulingInfo.MaxDeviationAfter)
								{
									requestsWithSameTemplate = GetNextConcurrentList(requestsWithSameTemplate, calculatedStartTime);
									requestsWithSameProfile  = GetNextConcurrentList(requestsWithSameProfile, calculatedStartTime);
								}
								else
								{
									// found that request cannot be scheduled - set expected start and end, after the while loop it will be signed as CouldNotBeScheduled
									AsLockable(serviceInstance).Unlock(_instanceLock);
									serviceInstance.SchedulingInfo.ExpectedStartTime = calculatedStartTime;
									serviceInstance.SchedulingInfo.ExpectedEndTime = calculatedEndTime;
									serviceInstance.SchedulingInfo.SchedulingStatus = SchedulingStatus.CouldNotBeScheduled;
									AsLockable(serviceInstance).Lock(_instanceLock);
									break;
								}
							}

							// check if scheduled inside max deviation frame
							if (serviceInstance.SchedulingInfo.ExpectedDeviation <= serviceInstance.SchedulingInfo.MaxDeviationAfter)
							{
								ScheduleServiceInstance(serviceInstance, avgExecutionTime);
							}
						}
						//------------------------------
						#endregion

						WriteLog(String.Format("Finish scheduling: scheduled={0} (uninitialized={1}, ended={2}), unscheduled={3}",
						                       _scheduledRequests.Count,
						                       _scheduledRequests.Count(s => s.State == ServiceState.Uninitialized),
						                       _scheduledRequests.Count(s => s.State == ServiceState.Ended), _unscheduledRequests.Count));

						// send the current list of scheduled services 
						SendScheduledServicesUpdate();
					}
				}

				// run shcheduled services
				ExecuteScheduledRequests();
			}
			catch (Exception ex)
			{
				WriteLog(String.Format("Failed in Schedule(), ex: {0}", ex.Message), ex);
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
				foreach (ServiceConfiguration configuration in _serviceConfigurationsToSchedule)
				{
					foreach (SchedulingRule schedulingRule in configuration.SchedulingRules)
					{
						foreach (TimeSpan time in schedulingRule.Times)
						{
							DateTime requestedTime = (_timeframeFrom.Date + time);

							while (requestedTime.Date <= _timeframeTo.Date)
							{
								if (IsRuleInTimeframe(schedulingRule, requestedTime))
								{
									// create service instance, check if it already exists, if not return it
									ServiceInstance request = CreateServiceInstance(configuration, schedulingRule, requestedTime);
									if (request != null)
									{
										if (!_unscheduledRequests.ContainsSignature(request) && !_scheduledRequests.ContainsSignature(request))
											yield return request;
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
								((requestedTime >= _timeframeFrom && requestedTime <= _timeframeTo) ||
								(requestedTime <= _timeframeFrom && 
								(requestedTime.Add(schedulingRule.MaxDeviationAfter) >= DateTime.Now)));

			return isRuleInTimeframe;
		}

		private ServiceInstance CreateServiceInstance(ServiceConfiguration config, SchedulingRule rule, DateTime requestedTime)
		{
			try
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
			catch (Exception ex)
			{
				WriteLog(String.Format("Failed create instance for service '{0}' rule '{1}', ex: {2}", config.DebugInfo(), rule.DebugInfo(), ex.Message), ex);
			}
			return null;
		}
		
		/// <summary>
		/// Start scheduled service instances if in time
		/// </summary>
		private void ExecuteScheduledRequests()
		{
			try
			{
				lock (_scheduledRequests)
				{
					//WriteLog("Start ExecuteScheduledRequests()");
					foreach (var request in _scheduledRequests.OrderBy(s => s.SchedulingInfo.ExpectedStartTime))
					{
						// check if it is time to execute request
						if ( request.SchedulingInfo.ExpectedStartTime <= DateTime.Now &&
							(request.SchedulingInfo.RequestedTime.Add(request.SchedulingInfo.MaxDeviationAfter) >= DateTime.Now) &&
							 request.SchedulingInfo.SchedulingStatus == SchedulingStatus.Scheduled)
						{
							// additional check for concurent services per template and profile wchich are scheduled for now or past
							int countedServicesWithSameConfiguration = _scheduledRequests.GetWithSameTemplate(request).Count(s => s.SchedulingInfo.ExpectedStartTime <= DateTime.Now);
							if (countedServicesWithSameConfiguration >= request.Configuration.Limits.MaxConcurrentPerTemplate)
								continue;

							int countedServicesWithSameProfile = _scheduledRequests.GetWithSameProfile(request).Count(s => s.SchedulingInfo.ExpectedStartTime <= DateTime.Now);
							if (countedServicesWithSameProfile >= request.Configuration.Limits.MaxConcurrentPerProfile)
								continue;

							// start service instance
							try
							{
								request.Start();
								WriteLog(String.Format("Started service '{0}'", request.DebugInfo()));
							}
							catch (Exception ex)
							{
								WriteLog(String.Format("Failed to start service '{0}', ex: {1}", request.DebugInfo(), ex.Message), ex);
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				WriteLog(String.Format("Failed in ExecuteScheduledRequests(), ex: {0}", ex.Message), ex);
			}
		}

		/// <summary>
		/// Load execution statistics per service/profile from environment
		/// </summary>
		private void LoadServiceExecutionStatistics()
		{
			if (Environment == null) return;
			try
			{
				_servicesExecutionStatisticsDict = Environment.GetServiceExecutionStatistics(Configuration.Percentile);
				WriteLog(String.Format("Loaded execution statistics for percentile {0}: {1} records loaded",Configuration.Percentile, ServicesExecutionStatisticsDict.Count));
			}
			catch (Exception ex)
			{
				WriteLog(String.Format("Failed in LoadServiceExecutionStatistics(), ex: {0}", ex.Message), ex);
			}
		}

		/// <summary>
		/// Get execution statistics per specific service and profile, if not exists returns default
		/// </summary>
		/// <param name="config">service configuration</param>
		/// <returns></returns>
		private TimeSpan GetExecutionStatisticsForService(ServiceConfiguration config)
		{
			var serviceConfigID = config.ConfigurationID;
			var profileID = config.Profile != null ? config.Profile.Parameters["AccountID"].ToString() : String.Empty;

			var key = String.Format("ConfigID:{0},ProfileID:{1}", serviceConfigID, profileID);

			return _servicesExecutionStatisticsDict.ContainsKey(key) ? TimeSpan.FromSeconds(_servicesExecutionStatisticsDict[key]) : Configuration.DefaultExecutionTime;
		}

		private int CountConcurrent(IEnumerable<ServiceInstance> serviceList, DateTime calculatedStartTime, DateTime calculatedEndTime)
		{
			return serviceList.Count(s =>(calculatedStartTime >= s.SchedulingInfo.ExpectedStartTime &&
								   calculatedStartTime <= s.SchedulingInfo.ExpectedEndTime) 
								   ||
								  (calculatedEndTime >= s.SchedulingInfo.ExpectedStartTime &&
								   calculatedEndTime <= s.SchedulingInfo.ExpectedEndTime));
		}

		private IOrderedEnumerable<ServiceInstance> GetNextConcurrentList(IEnumerable<ServiceInstance> serviceList, DateTime calculatedStartTime)
		{
			return	from s in serviceList
					where s.SchedulingInfo.ExpectedEndTime > calculatedStartTime
					orderby s.SchedulingInfo.ExpectedStartTime
					select s;
		}

		private void RemoveNotRelevantRequests()
		{
			// remove old not relevant scheduled request ended and could not be scheduled
			_scheduledRequests.RemoveByPredicate(request => (request.State == ServiceState.Ended &&
			                                                 request.SchedulingInfo.RequestedTime.Add(request.SchedulingInfo.MaxDeviationAfter) < DateTime.Now) ||
			                                                 request.SchedulingInfo.SchedulingStatus == SchedulingStatus.CouldNotBeScheduled);

			// remove from unscheduled request all requests which could not be scheduled and are out of timeframe by max deviation
			_unscheduledRequests.RemoveByPredicate(request => request.SchedulingInfo.SchedulingStatus == SchedulingStatus.CouldNotBeScheduled &&
												   request.SchedulingInfo.RequestedTime.Add(request.SchedulingInfo.MaxDeviationAfter) < DateTime.Now);
		}

		#endregion

		#region Private Methods

		/// <summary>
		/// In startup load services which are already executed from DB for recovery purpose
		/// in order not to execute already executed services
		/// </summary>
		private void LoadRecovery()
		{
			if (Environment == null) return;
			try
			{
				var instanceList = Environment.GetServiceInstanceActiveList();
				foreach (var instance in instanceList)
				{
					_scheduledRequests.Add(instance);
				}
			}
			catch (Exception ex)
			{
				WriteLog(String.Format("Failed in LoadRecovery(), ex: {0}", ex.Message), ex);
			}
		}

		private void AddRequestToSchedule(ServiceInstance request)
		{
			try
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

				WriteLog(String.Format("Add unplanned request to unscheduled list '{0}'", request.DebugInfo()));
				lock (_unscheduledRequests)
				{
					_unscheduledRequests.Add(request);
				}
				_needReschedule = true;
			}
			catch (Exception ex)
			{
				WriteLog(String.Format("Failed add unplanned request {0} to scheduler, ex: {1}", request.InstanceID, ex.Message), ex);
			}
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

		private void ServiceCannotBeScheduled(ServiceInstance request)
		{
			try
			{
				AsLockable(request).Unlock(_instanceLock);

				WriteLog(String.Format("Request '{0}' cannot be scheduled and its instance is aborted", request.DebugInfo()),
				         LogMessageType.Warning);
				request.SchedulingInfo.SchedulingStatus = SchedulingStatus.CouldNotBeScheduled;
				request.Abort();
			}
			catch (Exception ex)
			{
				WriteLog(String.Format("Failed to set service as unscheduled, ex: {0}", ex.Message), ex);
			}
			finally
			{
				// add to update UI with requests that cannot be scheduled, this request will be removed on the next Schedule() by RemoveNotRelevantRequests()
				_scheduledRequests.Add(request);
				AsLockable(request).Lock(_instanceLock);
			}
		}

		private void ScheduleServiceInstance(ServiceInstance serviceInstance, TimeSpan avgExecutionTime)
		{
			AsLockable(serviceInstance).Unlock(_instanceLock);
			serviceInstance.SchedulingInfo.SchedulingStatus = SchedulingStatus.Scheduled;
			serviceInstance.StateChanged += Instance_StateChanged;
			AsLockable(serviceInstance).Lock(_instanceLock);

			// set service instance max execution time
			serviceInstance.Configuration.Limits.MaxExecutionTime = TimeSpan.FromMilliseconds(avgExecutionTime.TotalMilliseconds * Configuration.MaxExecutionTimeFactor);

			if (!_scheduledRequests.ContainsSignature(serviceInstance))
			{
				WriteLog(String.Format("Move unscheduled request to scheduled list '{0}'", serviceInstance.DebugInfo()));
				_scheduledRequests.Add(serviceInstance);
				_unscheduledRequests.Remove(serviceInstance);
			}
			else
			{
				WriteLog(String.Format("Warning! Request '{0}' already exists in scheduled list", serviceInstance.DebugInfo()), LogMessageType.Warning);
			}
		}

		private void SendScheduledServicesUpdate()
		{
			try
			{
				Environment.SendScheduledServicesUpdate(_scheduledRequests.ToList());
			}
			catch (Exception ex)
			{
				WriteLog(String.Format("Failed to send scheduled updates, ex: {0}", ex.Message), ex);
			}
		}

		private void DebugStartupInfo()
		{
			foreach (var service in _serviceConfigurationsToSchedule)
			{
				var profile = service.GetProfileConfiguration() == null ? null : service.GetProfileConfiguration().Profile;
				foreach (var rule in service.SchedulingRules)
				{
					WriteLog(String.Format("Init: Profile {1}, Service {0}, Rule: scope={2}, time={3}, day={4}, max deviation after={5}, max deviation before={6}",
									service.ServiceName, profile != null ? profile.Name : String.Empty, rule.Scope, rule.Times[0], rule.Days[0],
									rule.MaxDeviationAfter, rule.MaxDeviationBefore));

					// check in Init if there are rules with MaxDeviationAfter == 0
					if (rule.MaxDeviationAfter == TimeSpan.Zero)
					{
						WriteLog(String.Format("Init: Max deviation after cannot be 0!!! Profile {1}, service {0}", 
							     service.ServiceName, profile != null ? profile.Name : String.Empty), LogMessageType.Error);
					}

				}
			}

			WriteLog(String.Format("Init: Service instances from recovery count={0}", _scheduledRequests.Count));
		}

		private void WriteLog(string message, LogMessageType logType = LogMessageType.Debug)
		{
			Debug.WriteLine("{0}: {1}", DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"), message);
			Log.Write(ToString(), message, logType);
		}

		private void WriteLog(string message,Exception ex)
		{
			Debug.WriteLine("{0}: {1}", DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"), message);
			Log.Write(ToString(), message, ex);
		}
		#endregion

		#region Events
		/// <summary>
		/// Add event from the environment to schedule service
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void Listener_ServiceRequiresScheduling(object sender, ServiceInstanceEventArgs e)
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
			if (instance.Outcome != ServiceOutcome.Canceled)	// more options?
			{
				AsLockable(instance).Unlock(_instanceLock);
				instance.SchedulingInfo.SchedulingStatus = SchedulingStatus.Activated;
				AsLockable(instance).Lock(_instanceLock);
			}
			//WriteLog(String.Format("Service '{0}' is {1}", InstanceRequestCollection.GetSignature(instance), instance.State.ToString()));
		}
		#endregion

		#region IDisposable
		public void Dispose()
		{
			if (_listener != null)
			{
				(_listener as IDisposable).Dispose();
			}
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

	public static class SerivceInstanceExtenstions
	{
		public static string DebugInfo(this ServiceInstance instance)
		{
			if (instance == null || instance.Configuration == null) return String.Empty;

			return String.Format("Service={0}, profile={1}, singnature={2}", 
								  instance.Configuration.ServiceName, 
								  instance.Configuration.Profile != null ? instance.Configuration.Profile.Name : String.Empty, 
							      InstanceRequestCollection.GetSignature(instance));
		}

	}

	public static class ServiceConfigurationExtenstions
	{
		public static string DebugInfo(this ServiceConfiguration serviceConfig)
		{
			if (serviceConfig == null) return String.Empty;

			var profile = serviceConfig.GetProfileConfiguration() == null ? null : serviceConfig.GetProfileConfiguration().Profile;
			return String.Format("Service={0}, profile={1}", serviceConfig.ServiceName, profile != null ? profile.Name : String.Empty);
		}
	}

	public static class SchedulingRuleExtenstions
	{
		public static string DebugInfo(this SchedulingRule rule)
		{
			if (rule == null) return String.Empty;

			return String.Format("Requested time={0}, scope={1}, max deviation after={2}",
								  rule.Scope, rule.Times.Length > 0 ? rule.Times[0].ToString(@"hh\:mm\:ss") : String.Empty, rule.MaxDeviationAfter.ToString(@"hh\:mm\:ss"));
		}
	}
	#endregion
}
