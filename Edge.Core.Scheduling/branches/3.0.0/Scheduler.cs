using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Core.Services.Configuration;
using Edge.Core.Services;
using System.Collections;
using System.Data.SqlClient;
using System.Threading;
using Edge.Core.Configuration;
using Edge.Core.Utilities;
using System.Security.Cryptography;
using Edge.Core.Scheduling;

namespace Edge.Core.Services.Scheduling
{
	public class Scheduler : MarshalByRefObject
	{
		#region members

		private object _instanceLock = new object();

		public ServiceEnvironment Environment { get; private set; }
		private ProfilesCollection _profiles = new ProfilesCollection();

		private Dictionary<string, ServiceConfiguration> _serviceBaseConfigurations = new Dictionary<string, ServiceConfiguration>();

		// Configurations from config file or from unplanned - 'Schedule' method goes over this to find things that need scheduling
		private List<ServiceConfiguration> _serviceConfigurationsToSchedule = new List<ServiceConfiguration>();

		// Unscheduled requests waiting to be scheduled
		private InstanceRequestCollection _unscheduledRequests = new InstanceRequestCollection();

		// Scheduled instances that are added by the 'Schedule' method
		private InstanceRequestCollection _scheduledRequests = new InstanceRequestCollection();

		private Dictionary<string, ServicePerProfileAvgExecutionTimeCash> _servicePerProfileAvgExecutionTimeCash = new Dictionary<string, ServicePerProfileAvgExecutionTimeCash>();
		DateTime _timeLineFrom;
		DateTime _timeLineTo;
		private TimeSpan _neededScheduleTimeLine; //scheduling for the next xxx min....
		private int _percentile = 80; //execution time of specifc service on sprcific Percentile
		private TimeSpan _intervalBetweenNewSchedule;
		private TimeSpan _findServicesToRunInterval;
		private TimeSpan _timeToDeleteServiceFromTimeLine;
		public event EventHandler<SchedulingRequestTimeArrivedArgs> ScheduledRequestTimeArrived;
		public event EventHandler<SchedulingInformationEventArgs> NewScheduleCreatedEvent;
		private volatile bool _needReschedule = false;
		private TimeSpan _executionTimeCashTimeOutAfter;
		private bool _started = false;
		Action _schedulerTimer;
		Action _RequiredServicesTimer;

		#endregion

		#region Properties
		public InstanceRequestCollection ScheduledServices
		{
			get { return _scheduledRequests; }
		}
		public IQueryable<ServiceConfiguration> ServiceConfigurations
		{
			get { return _serviceConfigurationsToSchedule.AsQueryable(); }
		}

		public ProfilesCollection Profiles
		{
			get { return _profiles; }
		}

		#endregion

		#region ManageScheduler
		/// <summary>
		/// Initialize all the services from configuration file or db4o
		/// </summary>
		/// <param name="getServicesFromConfigFile"></param>
		public Scheduler(ServiceEnvironment environment)
		{
			Environment = environment;

			_percentile = int.Parse(AppSettings.Get(this, "Percentile"));
			_neededScheduleTimeLine = TimeSpan.Parse(AppSettings.Get(this, "NeededScheduleTimeLine"));
			_intervalBetweenNewSchedule = TimeSpan.Parse(AppSettings.Get(this, "IntervalBetweenNewSchedule"));
			_findServicesToRunInterval = TimeSpan.Parse(AppSettings.Get(this, "FindServicesToRunInterval"));
			_timeToDeleteServiceFromTimeLine = TimeSpan.Parse(AppSettings.Get(this, "DeleteEndedServiceInterval"));
			_executionTimeCashTimeOutAfter = TimeSpan.Parse(AppSettings.Get(this, "DeleteEndedServiceInterval"));

			LoadServicesFromConfigurationFile();

		}

		/// <summary>
		/// start the timers of new scheduling and services required to run
		/// </summary>
		public void Start()
		{
			if (_started)
				return;
			_started = true;

			_schedulerTimer = StartSchedulerTimer;
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

			_RequiredServicesTimer = StartRequiredServicesTimer;
			_RequiredServicesTimer.BeginInvoke(result =>
			{
				try
				{
					_RequiredServicesTimer.EndInvoke(result);

				}
				catch (Exception ex)
				{

					Log.Write(this.ToString(), ex.Message, ex, LogMessageType.Error);
				}
			}, null);
		}

		private void StartSchedulerTimer()
		{
			Schedule(false);
			TimeSpan calcTimeInterval = _intervalBetweenNewSchedule;

			while (_started)
			{
				Thread.Sleep(TimeSpan.FromSeconds(5));

				if (_needReschedule || calcTimeInterval <= TimeSpan.Zero)
				{
					Schedule(false);
					calcTimeInterval = _intervalBetweenNewSchedule;
				}
				else
				{
					calcTimeInterval = calcTimeInterval.Subtract(TimeSpan.FromSeconds(5));
				}
			}
			return;
		}
		public void StartRequiredServicesTimer()
		{
			while (_started)
			{
				Thread.Sleep(_findServicesToRunInterval);//TODO: ADD CONST
				NotifyServicesToRun();
			}
			return;
		}


		/// <summary>
		///  stop the timers of new scheduling and services required to run
		/// </summary>
		public void Stop()
		{
			_started = false;

		}
		#endregion

		#region Configurations
		//==================================

		private Guid GuidFromString(string serviceName)
		{
			using (MD5 md5 = MD5.Create())
			{
				byte[] hash = md5.ComputeHash(Encoding.Default.GetBytes(serviceName));
				Guid result = new Guid(hash);
				return result;
			}
		}

		/// <summary>
		/// Load and translate the services from app.config
		/// </summary>
		private void LoadServicesFromConfigurationFile()
		{
			//base configuration
			foreach (ServiceElement serviceElement in EdgeServicesConfiguration.Current.Services)
			{
				ServiceConfiguration serviceConfiguration = new ServiceConfiguration()
				{
					ConfigurationID = GuidFromString(serviceElement.Name),
					ServiceName = serviceElement.Name
				};

				foreach (var option in serviceElement.Options)
					serviceConfiguration.Parameters.Add(option.Key, option.Value);

				serviceConfiguration.Limits.MaxConcurrentGlobal = serviceElement.MaxInstances;
				serviceConfiguration.Limits.MaxConcurrentPerProfile = serviceElement.MaxInstancesPerAccount;
				serviceConfiguration.ServiceClass = serviceElement.Class;
				
				foreach (SchedulingRule rule in serviceElement.SchedulingRules)
					serviceConfiguration.SchedulingRules.Add(rule);

				((ILockable)serviceConfiguration).Lock();

				_serviceBaseConfigurations.Add(serviceConfiguration.ServiceName, serviceConfiguration);
			}

			foreach (AccountElement account in EdgeServicesConfiguration.Current.Accounts)
			{

				// Create matching profile
				ServiceProfile profile = new ServiceProfile()
				{
					ProfileID = GuidFromString(account.ID.ToString())
				};
				profile.Parameters.Add("AccountID", account.ID);
				profile.Parameters.Add("AccountName", account.Name);
				foreach (AccountServiceElement accountService in account.Services)
				{
					
					ServiceElement serviceUse = accountService.Uses.Element;
				
					ServiceConfiguration deriveConfiguration = profile.DeriveConfiguration(_serviceBaseConfigurations[serviceUse.Name]);
					deriveConfiguration.ConfigurationID = GuidFromString(account.ID.ToString() + "___" + serviceUse.Name);


					foreach (SchedulingRule rule in serviceUse.SchedulingRules)
						deriveConfiguration.SchedulingRules.Add(rule);
					foreach (var option in serviceUse.Options)
						deriveConfiguration.Parameters[option.Key] = option.Value;
					deriveConfiguration.Limits.MaxConcurrentGlobal = serviceUse.MaxInstances;
					deriveConfiguration.Limits.MaxConcurrentPerProfile = serviceUse.MaxInstancesPerAccount;
					_serviceConfigurationsToSchedule.Add(deriveConfiguration);
					profile.Services.Add(deriveConfiguration);

				}
				_profiles.Add(profile);
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
			lock (_unscheduledRequests)
			{
				lock (_scheduledRequests)
				{
					// Set need reschedule to false in order to avoid more schedule from other threads
					_needReschedule = false;

					#region Manage history and find services to schedule
					// ------------------------------------

					// Move pending uninitialized services to the unscheduled list so they can be rescheduled
					foreach (ServiceInstance request in _scheduledRequests.RemoveNotActivated())
					{
						if (request.SchedulingInfo.RequestedTime + request.SchedulingInfo.MaxDeviationAfter > DateTime.Now)
						{
							_unscheduledRequests.Add(request);
						}
						else
						{
							AsLockable(request).Unlock(_instanceLock);
							request.SchedulingInfo.SchedulingStatus = SchedulingStatus.CouldNotBeScheduled;
							AsLockable(request).Lock(_instanceLock);
						}
					}

					// Get Services for next time line
					foreach (ServiceInstance request in GetServicesForTimeLine(reschedule))
						_unscheduledRequests.Add(request);

					// Copy unscheduled requests to an ordered list
					var servicesForNextTimeLine = new List<ServiceInstance>(_unscheduledRequests
						.OrderBy(schedulingdata => schedulingdata.SchedulingInfo.RequestedTime));

					// ------------------------------------
					#endregion

					#region Find Match services
					// ------------------------------------

					//Same services or same services with same profile
					foreach (ServiceInstance serviceInstance in servicesForNextTimeLine)
					{
						//Get all services with same configurationID
						var requestsWithSameConfiguration = _scheduledRequests.GetWithSameConfiguration(serviceInstance);

						//Get all services with same profileID
						var requestsWithSameProfile = _scheduledRequests.GetWithSameProfile(serviceInstance);

						//Find the first available time this service with specific service and profile
						TimeSpan avgExecutionTime = GetAverageExecutionTime(serviceInstance.Configuration.ServiceName, Convert.ToInt32(serviceInstance.Configuration.Profile.Parameters["AccountID"]), _percentile);

						DateTime baseStartTime = (serviceInstance.SchedulingInfo.RequestedTime < DateTime.Now) ? DateTime.Now : serviceInstance.SchedulingInfo.RequestedTime;
						DateTime baseEndTime = baseStartTime.Add(avgExecutionTime);
						DateTime calculatedStartTime = baseStartTime;
						DateTime calculatedEndTime = baseEndTime;

						bool found = false;
						while (!found)
						{
							IOrderedEnumerable<ServiceInstance> whereToLookNext = null;
							
							int countedPerConfiguration = requestsWithSameConfiguration.Count(s => (calculatedStartTime >= s.SchedulingInfo.ExpectedStartTime && calculatedStartTime <= s.SchedulingInfo.ExpectedEndTime) || (calculatedEndTime >= s.SchedulingInfo.ExpectedStartTime && calculatedEndTime <= s.SchedulingInfo.ExpectedEndTime));
							if (countedPerConfiguration < serviceInstance.Configuration.Limits.MaxConcurrentGlobal)
							{
								int countedPerProfile = requestsWithSameProfile.Count(s => (calculatedStartTime >= s.SchedulingInfo.ExpectedStartTime && calculatedStartTime <= s.SchedulingInfo.ExpectedEndTime) || (calculatedEndTime >= s.SchedulingInfo.ExpectedStartTime && calculatedEndTime <= s.SchedulingInfo.ExpectedEndTime));
								if (countedPerProfile < serviceInstance.Configuration.Limits.MaxConcurrentPerProfile)
								{
									AsLockable(serviceInstance).Unlock(_instanceLock);
									serviceInstance.SchedulingInfo.ExpectedStartTime = calculatedStartTime;
									serviceInstance.SchedulingInfo.ExpectedEndTime = calculatedEndTime;
									serviceInstance.SchedulingInfo.SchedulingStatus = Services.SchedulingStatus.Scheduled;
									AsLockable(serviceInstance).Lock(_instanceLock);

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

								calculatedStartTime = whereToLookNext.Where(s => s.SchedulingInfo.ExpectedEndTime >= calculatedStartTime).Min(s => s.SchedulingInfo.ExpectedEndTime);
								if (calculatedStartTime < DateTime.Now)
									calculatedStartTime = DateTime.Now;

								//Get end time
								calculatedEndTime = calculatedStartTime.Add(avgExecutionTime);

								////remove unfree time from servicePerConfiguration and servicePerProfile							
								if (calculatedStartTime <= _timeLineTo)
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
							serviceInstance.StateChanged += new EventHandler(Instance_StateChanged);							
							AsLockable(serviceInstance).Lock(_instanceLock);

							// Legacy stuff
							//TODO: MAXEXECUTIONTIME
							TimeSpan maxExecutionTime = TimeSpan.FromMilliseconds(avgExecutionTime.TotalMilliseconds * double.Parse(AppSettings.Get(this, "MaxExecutionTimeProduct")));
							serviceInstance.Configuration.Limits.MaxExecutionTime = maxExecutionTime;
							_scheduledRequests.Add(serviceInstance);
							_unscheduledRequests.Remove(serviceInstance);
						}

					}
					#endregion
				}


				SchedulingInformationEventArgs args = new SchedulingInformationEventArgs();
				args.ScheduleInformation = new List<ServiceInstance>();
				foreach (var scheduleService in _scheduledRequests)
				{
					args.ScheduleInformation.Add(scheduleService);

				}
				OnNewScheduleCreated(args);
				NotifyServicesToRun();
			}
		}

		void Instance_StateChanged(object sender, EventArgs e)
		{
			var instance = (ServiceInstance)sender;
			AsLockable(instance).Unlock(_instanceLock);
			instance.SchedulingInfo.SchedulingStatus = SchedulingStatus.Activated;
			AsLockable(instance).Lock(_instanceLock);
		}

		void Instance_OutcomeReported(object sender, EventArgs e)
		{
			_needReschedule = true;
		}



		/// <summary>
		/// Get this time line services 
		/// </summary>
		/// <param name="useCurrentTimeline">if it's for reschedule then the time line is the same as the last schedule</param>
		/// <returns></returns>
		private IEnumerable<ServiceInstance> GetServicesForTimeLine(bool useCurrentTimeline)
		{
			// Take next timeline if false
			if (!useCurrentTimeline)
			{
				_timeLineFrom = DateTime.Now;
				_timeLineTo = DateTime.Now.Add(_neededScheduleTimeLine);
			}

			lock (_serviceConfigurationsToSchedule)
			{
				for (int i = 0; i < _serviceConfigurationsToSchedule.Count; i++)
				{
					ServiceConfiguration configuration = _serviceConfigurationsToSchedule[i];

					foreach (SchedulingRule schedulingRule in configuration.SchedulingRules)
					{
						bool ruleSuitable = false;
						foreach (TimeSpan time in schedulingRule.Times)
						{
							DateTime requestedTime = (_timeLineFrom.Date + time).RemoveSeconds();

							while (requestedTime.Date <= _timeLineTo.Date)
							{
								switch (schedulingRule.Scope)
								{
									case SchedulingScope.Day:
										ruleSuitable = true;
										break;
									case SchedulingScope.Week:
										int dayOfWeek = (int)requestedTime.DayOfWeek + 1;
										if (schedulingRule.Days.Contains(dayOfWeek))
											ruleSuitable = true;
										break;
									case SchedulingScope.Month:
										int dayOfMonth = requestedTime.Day;
										if (schedulingRule.Days.Contains(dayOfMonth))
											ruleSuitable = true;
										break;
								}

								if ((ruleSuitable) &&
									(requestedTime >= _timeLineFrom && requestedTime <= _timeLineTo) ||
									(requestedTime <= _timeLineFrom && (schedulingRule.MaxDeviationAfter == TimeSpan.Zero || requestedTime.Add(schedulingRule.MaxDeviationAfter) >= DateTime.Now))
									)
								{

									ServiceInstance request = Environment.NewServiceInstance(configuration);
									request.SchedulingInfo = new SchedulingInfo()
									{
										SchedulingStatus = SchedulingStatus.New,
										SchedulingScope = schedulingRule.Scope,
										RequestedTime = requestedTime,
										MaxDeviationBefore = schedulingRule.MaxDeviationBefore,
										MaxDeviationAfter = schedulingRule.MaxDeviationAfter,
									};
									AsLockable(request).Lock(_instanceLock);

									if (!_unscheduledRequests.ContainsSignature(request) && !_scheduledRequests.ContainsSignature(request))
										yield return request;
								}
								requestedTime = requestedTime.AddDays(1);
							}
						}
					}
				}

			}
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
						using (SqlCommand sqlCommand = new SqlCommand("ServiceConfiguration_GetExecutionTime"))
						{
							sqlCommand.Connection = SqlConnection;
							sqlCommand.CommandType = System.Data.CommandType.StoredProcedure;
							sqlCommand.Parameters.AddWithValue("@ConfigName", configurationName);
							sqlCommand.Parameters.AddWithValue("@Percentile", Percentile);
							sqlCommand.Parameters.AddWithValue("@ProfileID", AccountID);

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
		public void AddRequestToSchedule(ServiceInstance request)
		{
			lock (_unscheduledRequests)
			{
				_unscheduledRequests.Add(request);
			}
			_needReschedule = true;
		}
		public void AddChildServiceToSchedule(ServiceInstance instance)
		{
			ServiceConfiguration baseConfiguration;
			if (!_serviceBaseConfigurations.TryGetValue(instance.Configuration.ServiceName, out baseConfiguration))
				throw new KeyNotFoundException(String.Format("No base configuration exists for the service '{0}'.", instance.Configuration.ServiceName));

			ServiceProfile profile;
			if (!_profiles.TryGetValue(Convert.ToInt32(instance.Configuration.Profile.Parameters["AccountID"]), out profile))
				throw new KeyNotFoundException(String.Format("No profile exists with the ID '{0}' (account ID).", (instance.Configuration.Profile.Parameters["AccountID"])));

			if (instance.Configuration.SchedulingRules[0].Scope!=SchedulingScope.Unplanned)
				throw new Exception("instance rule is not unnplaned, scheduler only get services");
			AsLockable(instance).Unlock(_instanceLock);
			instance.SchedulingInfo.SchedulingStatus = Services.SchedulingStatus.New;
			instance.SchedulingInfo.RequestedTime = DateTime.Now;
			instance.SchedulingInfo.SchedulingScope = SchedulingScope.Unplanned;
			AsLockable(instance).Lock(_instanceLock);

			AddRequestToSchedule(instance);
		}
		/// <summary>
		/// Delete specific instance of service (service for specific time not all the services)
		/// </summary>
		/// <param name="schedulingRequest"></param>
		public void CancelSchedulingRequest(ServiceInstance schedulingRequest)
		{

			throw new NotImplementedException();
			//if(_schedulingRequests.ContainsSimilar(schedulingRequest))

			//_schedulingRequests[schedulingRequest].Canceled = true;
		}

		private void NotifyServicesToRun()
		{
			lock (_scheduledRequests)
			{
				foreach (var request in _scheduledRequests.OrderBy(s => s.SchedulingInfo.ExpectedStartTime))
				{

					if (
						request.SchedulingInfo.ExpectedStartTime <= DateTime.Now &&
						(request.SchedulingInfo.MaxDeviationAfter == TimeSpan.Zero || request.SchedulingInfo.RequestedTime.Add(request.SchedulingInfo.MaxDeviationAfter) >= DateTime.Now) &&
						request.SchedulingInfo.SchedulingStatus == SchedulingStatus.Scheduled
					)
					{
						int countedServicesWithSameConfiguration = _scheduledRequests.GetWithSameConfiguration(request).Count();
						if (countedServicesWithSameConfiguration >= request.Configuration.Limits.MaxConcurrentGlobal)
							continue;

						int countedServicesWithSameProfile = _scheduledRequests.GetWithSameProfile(request).Count();
						if (countedServicesWithSameProfile >= request.Configuration.Limits.MaxConcurrentPerProfile)
							continue;

						//Log.Write(this.ToString(), string.Format("Service {0} required to run", request.Configuration.ServiceName), LogMessageType.Information);

						if (ScheduledRequestTimeArrived != null)
							ScheduledRequestTimeArrived(this, new SchedulingRequestTimeArrivedArgs() { Request = request });
					}
				}
			}
		}

		/// <summary>
		/// set event new schedule created
		/// </summary>
		/// <param name="e"></param>
		private void OnNewScheduleCreated(SchedulingInformationEventArgs e)
		{
			if (NewScheduleCreatedEvent != null)
				NewScheduleCreatedEvent(this, e);
		}

		private static ILockable AsLockable(ILockable obj)
		{
			return obj;
		}

		//==================================

		#endregion

	}

	#region eventargs classes
	public class SchedulingRequestTimeArrivedArgs : EventArgs
	{
		public ServiceInstance Request;
	}

	public class SchedulingInformationEventArgs : EventArgs
	{
		public List<ServiceInstance> ScheduleInformation;
	}
	#endregion

	#region extensions
	//public static class DictionaryExtensions
	//{
	//    public static IEnumerable<KeyValuePair<TKey, TValue>> RemoveAll<TKey, TValue>(this Dictionary<TKey, TValue> dict,
	//                                 Func<KeyValuePair<TKey, TValue>, bool> condition)
	//    {
	//        foreach (var cur in dict.Where(condition).ToList())
	//        {
	//            dict.Remove(cur.Key);
	//            yield return cur;
	//        }
	//    }
	//}
	//public static class SchedulingRequestCollectionExtensions
	//{
	//    public static IEnumerable<SchedulingRequest> RemoveAll(this SchedulingRequestCollection req,
	//                                 Func<SchedulingRequest, bool> condition)
	//    {
	//        foreach (var cur in req.Where(condition))
	//        {
	//            req.Remove(cur);
	//            yield return cur;
	//        }
	//    }
	//}


	public static class DateTimeExtenstions
	{
		public static DateTime RemoveSeconds(this DateTime time)
		{
			return new DateTime(time.Year, time.Month, time.Day, time.Hour, time.Minute, 0, 0);
		}
	}
	#endregion
}
