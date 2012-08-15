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
		private ProfilesCollection _profiles = new ProfilesCollection();

		private Dictionary<string, ServiceConfiguration> _serviceBaseConfigurations = new Dictionary<string, ServiceConfiguration>();

		// Configurations from config file or from unplanned - 'Schedule' method goes over this to find things that need scheduling
		private List<ServiceConfiguration> _serviceConfigurationsToSchedule = new List<ServiceConfiguration>();

		// Unscheduled requests waiting to be scheduled
		private SchedulingRequestCollection _unscheduledRequests = new SchedulingRequestCollection();

		// Scheduled instances that are added by the 'Schedule' method
		private SchedulingRequestCollection _scheduledRequests = new SchedulingRequestCollection();

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
		public SchedulingRequestCollection ScheduledServices
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
		public Scheduler(bool getServicesFromConfigFile)
		{
			if (getServicesFromConfigFile)
				LoadServicesFromConfigurationFile();

			_percentile = int.Parse(AppSettings.Get(this, "Percentile"));
			_neededScheduleTimeLine = TimeSpan.Parse(AppSettings.Get(this, "NeededScheduleTimeLine"));
			_intervalBetweenNewSchedule = TimeSpan.Parse(AppSettings.Get(this, "IntervalBetweenNewSchedule"));
			_findServicesToRunInterval = TimeSpan.Parse(AppSettings.Get(this, "FindServicesToRunInterval"));
			_timeToDeleteServiceFromTimeLine = TimeSpan.Parse(AppSettings.Get(this, "DeleteEndedServiceInterval"));
			_executionTimeCashTimeOutAfter = TimeSpan.Parse(AppSettings.Get(this, "DeleteEndedServiceInterval"));

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
						{"AccountID", account.ID},
						{"AccountName",account.Name},
					},
					ServiceConfigurations = new List<ServiceConfiguration>()
				};
				_profiles.Add(profile);

				foreach (AccountServiceElement accountService in account.Services)
				{
					ServiceConfiguration serviceConfiguration = ServiceConfiguration.FromLegacyConfiguration(
						accountService,
						_serviceBaseConfigurations[accountService.Uses.Element.Name],
						profile
					);
					serviceConfiguration.Lock();

					profile.ServiceConfigurations.Add(serviceConfiguration);
					_serviceConfigurationsToSchedule.Add(serviceConfiguration);

					// TODO: load relevant history for this service
					foreach (SchedulingRule rule in serviceConfiguration.SchedulingRules)
					{
						// TODO: select the saved scheduling requests from the DB according to the scope of the fule
						//throw new NotImplementedException();
					}
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
			lock (_unscheduledRequests)
			{
				lock (_scheduledRequests)
				{
					// Set need reschedule to false in order to avoid more schedule from other threads
					_needReschedule = false;

					#region Manage history and find services to schedule
					// ------------------------------------

					// Move pending uninitialized services to the unscheduled list so they can be rescheduled
					foreach (SchedulingRequest request in _scheduledRequests.RemoveNotActivated())
					{
						if (request.RequestedTime + request.Rule.MaxDeviationAfter > DateTime.Now)
						{
							_unscheduledRequests.Add(request);
						}
						else
						{
							request.SchedulingStatus = SchedulingStatus.Expired;
						}
					}

					// Get Services for next time line
					foreach (SchedulingRequest request in GetServicesForTimeLine(reschedule))
						_unscheduledRequests.Add(request);

					// Copy unscheduled requests to an ordered list
					var servicesForNextTimeLine = new List<SchedulingRequest>(_unscheduledRequests
						.OrderBy(schedulingdata => schedulingdata.RequestedTime)
						.ThenByDescending(schedulingdata => schedulingdata.Configuration.Priority)
					);

					// ------------------------------------
					#endregion

					#region Find Match services
					// ------------------------------------

					//Same services or same services with same profile
					foreach (SchedulingRequest schedulingRequest in servicesForNextTimeLine)
					{
						//Get all services with same configurationID
						var requestsWithSameConfiguration = _scheduledRequests.GetWithSameConfiguration(schedulingRequest);
						
						//Get all services with same profileID
						var requestsWithSameProfile = _scheduledRequests.GetWithSameProfile(schedulingRequest);

						//Find the first available time this service with specific service and profile
						TimeSpan avgExecutionTime = GetAverageExecutionTime(schedulingRequest.Configuration.Name, schedulingRequest.Configuration.Profile.ID, _percentile);

						DateTime baseStartTime = (schedulingRequest.RequestedTime < DateTime.Now) ? DateTime.Now : schedulingRequest.RequestedTime;
						DateTime baseEndTime = baseStartTime.Add(avgExecutionTime);
						DateTime calculatedStartTime = baseStartTime;
						DateTime calculatedEndTime = baseEndTime;

						bool found = false;
						while (!found)
						{
							IOrderedEnumerable<SchedulingRequest> whereToLookNext = null;

							int countedPerConfiguration = requestsWithSameConfiguration.Count(s => (calculatedStartTime >= s.ScheduledStartTime && calculatedStartTime <= s.ScheduledEndTime) || (calculatedEndTime >= s.ScheduledStartTime && calculatedEndTime <= s.ScheduledEndTime));
							if (countedPerConfiguration < schedulingRequest.Configuration.MaxConcurrent)
							{
								int countedPerProfile = requestsWithSameProfile.Count(s => (calculatedStartTime >= s.ScheduledStartTime && calculatedStartTime <= s.ScheduledEndTime) || (calculatedEndTime >= s.ScheduledStartTime && calculatedEndTime <= s.ScheduledEndTime));
								if (countedPerProfile < schedulingRequest.Configuration.MaxConcurrentPerProfile)
								{
									if (!(schedulingRequest.Configuration is ServiceInstanceConfiguration))
									{
										// Not a child instance, so we need to create a new instance
										ServiceInstance serviceInstance = ServiceInstance.FromLegacyInstance(
											Legacy.Service.CreateInstance(schedulingRequest.Configuration.LegacyConfiguration, int.Parse(schedulingRequest.Configuration.Profile.Settings["AccountID"].ToString())),
											schedulingRequest.Configuration
										);

										// Make the request point to the instance configuration now that we have it
										schedulingRequest.Configuration = serviceInstance.Configuration;
									}

									schedulingRequest.ScheduledStartTime = calculatedStartTime;
									schedulingRequest.ScheduledEndTime = calculatedEndTime;

									schedulingRequest.Instance.SchedulingRequest = schedulingRequest;
									schedulingRequest.Instance.StateChanged += new EventHandler(Instance_StateChanged);
									schedulingRequest.Instance.OutcomeReported += new EventHandler(Instance_OutcomeReported);
									
									// Legacy stuff
									TimeSpan maxExecutionTime = TimeSpan.FromMilliseconds(avgExecutionTime.TotalMilliseconds * double.Parse(AppSettings.Get(this, "MaxExecutionTimeProduct")));
									schedulingRequest.Configuration.MaxExecutionTime = maxExecutionTime;

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

								calculatedStartTime = whereToLookNext.Where(s => s.ScheduledEndTime >= calculatedStartTime).Min(s => s.ScheduledEndTime);
								if (calculatedStartTime < DateTime.Now)
									calculatedStartTime = DateTime.Now;

								//Get end time
								calculatedEndTime = calculatedStartTime.Add(avgExecutionTime);

								////remove unfree time from servicePerConfiguration and servicePerProfile							
								if (calculatedStartTime <= _timeLineTo)
								{
									requestsWithSameConfiguration = from s in requestsWithSameConfiguration
																	where s.ScheduledEndTime > calculatedStartTime
																	orderby s.ScheduledStartTime
																	select s;

									requestsWithSameProfile = from s in requestsWithSameProfile
															  where s.ScheduledEndTime > calculatedStartTime
															  orderby s.ScheduledStartTime
															  select s;
								}
							}
						}

						if (schedulingRequest.ActualDeviation <= schedulingRequest.Rule.MaxDeviationAfter || schedulingRequest.Rule.MaxDeviationAfter == TimeSpan.Zero)
						{
							_scheduledRequests.Add(schedulingRequest);
							_unscheduledRequests.Remove(schedulingRequest);
							schedulingRequest.SchedulingStatus = SchedulingStatus.Scheduled;
						}
					}
					#endregion
				}
					

				SchedulingInformationEventArgs args = new SchedulingInformationEventArgs();
				args.ScheduleInformation = new List<SchedulingRequest>();
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
		}

		void Instance_OutcomeReported(object sender, EventArgs e)
		{
			ServiceInstance instance = (ServiceInstance)sender;
			_needReschedule = true;

			instance.SchedulingRequest.SchedulingStatus = SchedulingStatus.Activated;
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

			lock (_serviceConfigurationsToSchedule)
			{
				for (int i = 0; i < _serviceConfigurationsToSchedule.Count; i++)
				{
					ServiceConfiguration service = _serviceConfigurationsToSchedule[i];

					foreach (SchedulingRule schedulingRule in service.SchedulingRules)
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
											ruleSuitable=true;
										break;
									case SchedulingScope.Week:
										int dayOfWeek = (int)requestedTime.DayOfWeek + 1;
										if (schedulingRule.Days.Contains(dayOfWeek)) 
											ruleSuitable=true;
										break;
									case SchedulingScope.Month:
										int dayOfMonth = requestedTime.Day;
										if (schedulingRule.Days.Contains(dayOfMonth))
											ruleSuitable=true;
										break;
								}

								if ((ruleSuitable) &&
									(requestedTime >= _timeLineFrom && requestedTime <= _timeLineTo) ||
									(requestedTime <= _timeLineFrom && (schedulingRule.MaxDeviationAfter == TimeSpan.Zero || requestedTime.Add(schedulingRule.MaxDeviationAfter) >= DateTime.Now))
									)
								{
									SchedulingRequest request = new SchedulingRequest(service, schedulingRule, requestedTime);
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
		public void AddRequestToSchedule(SchedulingRequest request)
		{
			lock (_unscheduledRequests)
			{
				_unscheduledRequests.Add(request);
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

			SchedulingRequest request = new SchedulingRequest(childInstance, new SchedulingRule()
			{				
				SpecificDateTime = DateTime.Now,
				Scope = SchedulingScope.Unplanned,
				MaxDeviationAfter = TimeSpan.FromHours(1)
			}, DateTime.Now);

			AddRequestToSchedule(request);
		}
		/// <summary>
		/// Delete specific instance of service (service for specific time not all the services)
		/// </summary>
		/// <param name="schedulingRequest"></param>
		public void CancelSchedulingRequest(SchedulingRequest schedulingRequest)
		{

			throw new NotImplementedException();
			//if(_schedulingRequests.ContainsSimilar(schedulingRequest))

			//_schedulingRequests[schedulingRequest].Canceled = true;
		}

		private void NotifyServicesToRun()
		{
			lock (_scheduledRequests)
			{
				foreach (var request in _scheduledRequests.OrderBy(s => s.ScheduledStartTime))
				{
					
					if (
						request.ScheduledStartTime <= DateTime.Now &&
						(request.Rule.MaxDeviationAfter == TimeSpan.Zero || request.RequestedTime.Add(request.Rule.MaxDeviationAfter) >= DateTime.Now) &&
						request.Instance.State == Legacy.ServiceState.Uninitialized
					)
					{
						int countedServicesWithSameConfiguration = _scheduledRequests.GetWithSameConfiguration(request).Count();
						if (countedServicesWithSameConfiguration >= request.Configuration.MaxConcurrent)
							continue;

						int countedServicesWithSameProfile = _scheduledRequests.GetWithSameProfile(request).Count();
						if (countedServicesWithSameProfile >= request.Configuration.MaxConcurrentPerProfile)
							continue;

						Log.Write(this.ToString(), string.Format("Service {0} required to run", request.Configuration.Name), LogMessageType.Information);

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

		//==================================

		#endregion

	}

	#region eventargs classes
	public class SchedulingRequestTimeArrivedArgs : EventArgs
	{
		public SchedulingRequest Request;
	}

	public class SchedulingInformationEventArgs : EventArgs
	{
		public List<SchedulingRequest> ScheduleInformation;
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
