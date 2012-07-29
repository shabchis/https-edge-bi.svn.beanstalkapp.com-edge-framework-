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
		private List<SchedulingRequest> _tempCompletedRequests;
		private ProfilesCollection _profiles = new ProfilesCollection();

		private Dictionary<string, ServiceConfiguration> _serviceBaseConfigurations = new Dictionary<string, ServiceConfiguration>();

		// Configurations from config file or from unplanned - 'Schedule' method goes over this to find things that need scheduling
		private List<ServiceConfiguration> _serviceConfigurationsToSchedule = new List<ServiceConfiguration>();

		// Scheduled instances that are added by the 'Schedule' method
		//private ScheduledServiceCollection _scheduledServices = new ScheduledServiceCollection();
		private SchedulingRequestCollection _schedulingRequests = new SchedulingRequestCollection();

		private Dictionary<string, ServicePerProfileAvgExecutionTimeCash> _servicePerProfileAvgExecutionTimeCash = new Dictionary<string, ServicePerProfileAvgExecutionTimeCash>();
		DateTime _timeLineFrom;
		DateTime _timeLineTo;
		private TimeSpan _neededScheduleTimeLine; //scheduling for the next xxx min....
		private int _percentile = 80; //execution time of specifc service on sprcific Percentile
		private TimeSpan _intervalBetweenNewSchedule;
		private TimeSpan _findServicesToRunInterval;		
		private TimeSpan _timeToDeleteServiceFromTimeLine;	
		public event EventHandler<ServicesToRunEventArgs> ServiceRunRequiredEvent;
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
			get { return _schedulingRequests; }
		}
		public IQueryable<ServiceConfiguration> ServiceConfigurations
		{
			get { return _serviceConfigurationsToSchedule.AsQueryable(); }
		}
		public SchedulerState SchedulerState
		{
			get { return _state; }
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

			_state = new SchedulerState();
			_state.Load();
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
			if (_tempCompletedRequests == null)
				_tempCompletedRequests = new List<SchedulingRequest>();
			Schedule(true);
			TimeSpan calcTimeInterval = _intervalBetweenNewSchedule;
			while (_started)
			{

				Thread.Sleep(TimeSpan.FromSeconds(5));
				if (_tempCompletedRequests.Count > 0 || _needReschedule==true || calcTimeInterval == TimeSpan.Zero)
				{
					Schedule(false);
					lock (_tempCompletedRequests)
					{
						foreach (var request in _tempCompletedRequests)
						{
							if (_serviceConfigurationsToSchedule.Contains(request.Configuration) && request.Rule.Scope==SchedulingScope.Unplanned)
								_serviceConfigurationsToSchedule.Remove(request.Configuration);
						}
						_tempCompletedRequests.Clear();
					}
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
						throw new NotImplementedException();
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
			lock (_serviceConfigurationsToSchedule)
			{
				lock (_schedulingRequests)
				{
					// Set need reschedule to false in order to avoid more schedule from other threads
					_needReschedule = false;

					#region Manage history and find services to schedule
					// ------------------------------------

					// TODO: this is PROBABALY unnecessary but check
					/*
					// Move to history ended/canceled items
					lock (_state.HistoryItems)
					{
						// clear services from history that already ran and their maxdiviation pass -> this is to enable to run them again if their is another schedule
						_state.HistoryItems.RemoveAll(k => k.Value.TimeToRun.Add(k.Value.MaxDeviationAfter) < DateTime.Now);

						foreach (var scheduleService in _schedulingRequests.RemoveAll(k => k.Canceled == true || k.LegacyInstance.State == Legacy.ServiceState.Ended))
							_state.HistoryItems.Add(scheduleService.SchedulingRequest.GetHashCode(), HistoryItem.FromSchedulingData(scheduleService.SchedulingRequest, scheduleService, SchedulingResult.Canceled));

						_state.Save();
					}
					 * */

					// Remove pending uninitialized services so they can be rescheduled
					_schedulingRequests.RemoveAll(k => k.Instance.LegacyInstance.State == Legacy.ServiceState.Uninitialized && k.RequestedTime.Add(k.Rule.MaxDeviationAfter) > DateTime.Now);


					//Get Services for next time line					
					IEnumerable<SchedulingRequest> servicesForNextTimeLine = GetServicesForTimeLine(reschedule);

					// ------------------------------------
					#endregion

					#region Find Match services
					// ------------------------------------

					//Same services or same services with same profile
					foreach (SchedulingRequest schedulingRequest in servicesForNextTimeLine)
					{
						// NOT NEEDED because we already checked this in GetServicesForTimeLine
						/*
						//if key exist then this service is runing or ended and should nt be schedule again
						//if (!_schedulingRequests.ContainsKey(schedulingRequest))
						//{
						*/
						//Get all services with same configurationID
						var servicesWithSameConfiguration =
							from s in _schedulingRequests
							where
								s.Configuration.Name == schedulingRequest.Configuration.BaseConfiguration.Name && //should be id but no id yet
								s.Instance.LegacyInstance.State != Legacy.ServiceState.Ended &&
								s.Instance.Canceled == false //runnig or not started yet
							orderby s.Instance.ExpectedStartTime ascending
							select s;

						//Get all services with same profileID
						var servicesWithSameProfile =
							from s in _schedulingRequests
							where
								s.Configuration.Profile == schedulingRequest.Configuration.Profile &&
								s.Configuration.Name == schedulingRequest.Configuration.BaseConfiguration.Name &&
								s.Instance.LegacyInstance.State != Legacy.ServiceState.Ended &&
								s.Instance.Canceled == false //not deleted
							orderby s.Instance.ExpectedStartTime ascending
							select s;

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

							int countedPerConfiguration = servicesWithSameConfiguration.Count(s => (calculatedStartTime >= s.Instance.ExpectedStartTime && calculatedStartTime <= s.Instance.ExpectedEndTime) || (calculatedEndTime >= s.Instance.ExpectedStartTime && calculatedEndTime <= s.Instance.ExpectedEndTime));
							if (countedPerConfiguration < schedulingRequest.Configuration.MaxConcurrent)
							{
								int countedPerProfile = servicesWithSameProfile.Count(s => (calculatedStartTime >= s.Instance.ExpectedStartTime && calculatedStartTime <= s.Instance.ExpectedEndTime) || (calculatedEndTime >= s.Instance.ExpectedStartTime && calculatedEndTime <= s.Instance.ExpectedEndTime));
								if (countedPerProfile < schedulingRequest.Configuration.MaxConcurrentPerProfile)
								{

									if (!(schedulingRequest.Configuration is ServiceInstanceConfiguration))
									{
										ServiceInstance serviceInstance = ServiceInstance.FromLegacyInstance(
											Legacy.Service.CreateInstance(schedulingRequest.Configuration.LegacyConfiguration, int.Parse(schedulingRequest.Configuration.Profile.Settings["AccountID"].ToString())),
											schedulingRequest.Configuration
										);

										schedulingRequest.Configuration = serviceInstance.Configuration;
									}

									schedulingRequest.Instance.SchedulingRequest = schedulingRequest;
									schedulingRequest.Instance.SchedulingAccuracy = _percentile;
									schedulingRequest.Instance.ExpectedStartTime = calculatedStartTime;
									schedulingRequest.Instance.ExpectedEndTime = calculatedEndTime;
									schedulingRequest.Instance.OutcomeReported += new EventHandler(Instance_OutcomeReported);
									// Legacy stuff
									TimeSpan maxExecutionTime = TimeSpan.FromMilliseconds(avgExecutionTime.TotalMilliseconds * double.Parse(AppSettings.Get(this, "MaxExecutionTimeProduct")));
									schedulingRequest.Configuration.MaxExecutionTime = maxExecutionTime;

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

								calculatedStartTime = whereToLookNext.Where(s => s.Instance.ExpectedEndTime >= calculatedStartTime).Min(s => s.Instance.ExpectedEndTime);
								if (calculatedStartTime < DateTime.Now)
									calculatedStartTime = DateTime.Now;

								//Get end time
								calculatedEndTime = calculatedStartTime.Add(avgExecutionTime);

								////remove unfree time from servicePerConfiguration and servicePerProfile							
								if (calculatedStartTime <= _timeLineTo)
								{
									servicesWithSameConfiguration = from s in servicesWithSameConfiguration
																	where s.Instance.ExpectedEndTime > calculatedStartTime
																	orderby s.Instance.ExpectedStartTime
																	select s;

									servicesWithSameProfile = from s in servicesWithSameProfile
															  where s.Instance.ExpectedEndTime > calculatedStartTime
															  orderby s.Instance.ExpectedStartTime
															  select s;
								}
							}
						}


						if (schedulingRequest.Instance.ActualDeviation <= schedulingRequest.Rule.MaxDeviationAfter || schedulingRequest.Rule.MaxDeviationAfter == TimeSpan.Zero)
						{
							_schedulingRequests.Add(schedulingRequest);
							schedulingRequest.SchedulingStatus = SchedulingStatus.Scheduled;
							schedulingRequest.Save();
						}
						else
						{
							schedulingRequest.SchedulingStatus = SchedulingStatus.WillNotRun;
							schedulingRequest.Save();

						}
					}
				}
					#endregion

				SchedulingInformationEventArgs args = new SchedulingInformationEventArgs();
				args.ScheduleInformation = new List<SchedulingRequest>();
				foreach (var scheduleService in _schedulingRequests)
				{
					args.ScheduleInformation.Add(scheduleService);

				}
				OnNewScheduleCreated(args);
				NotifyServicesToRun();
			}
		}

		void Instance_OutcomeReported(object sender, EventArgs e)
		{
			ServiceInstance instance=(ServiceInstance)sender;
			_tempCompletedRequests.Add(instance.SchedulingRequest);
			instance.SchedulingRequest.SchedulingStatus = SchedulingStatus.Done;
			instance.SchedulingRequest.Save();

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
									SchedulingRequest request = new SchedulingRequest(service,schedulingRule,requestedTime);
									

									// special for unplanned
									if (schedulingRule.Scope == SchedulingScope.Unplanned)
									{
										//schedulingdata.GuidForUnplanned = schedulingRule.GuidForUnplanned;
										request.Rule.MaxDeviationAfter = TimeSpan.FromHours(8);
									}

									potentialSchedulingdata.Add(request);
								}
								requestedTime = requestedTime.AddDays(1);
							}
						}


					}
				}

			}
			// Move potential to final only if its not already scheduled and if its not in the history
			foreach (SchedulingRequest request in potentialSchedulingdata)
			{				
				if (!_schedulingRequests.ContainsSimilar(request))
					finalSchedulingdata.Add(request);

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
			lock (_serviceConfigurationsToSchedule)
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

			throw new NotImplementedException();
			//if(_schedulingRequests.ContainsSimilar(schedulingRequest))
				
			//_schedulingRequests[schedulingRequest].Canceled = true;
		}
		private void NotifyServicesToRun()
		{
			//DO some checks
			var instancesShouldRun =new List<SchedulingRequest>();
			List<ServiceInstance> instancesToRun = new List<ServiceInstance>();

			lock (_schedulingRequests)
			{
				foreach (var scheduleService in _schedulingRequests.OrderBy(s => s.Instance.ExpectedStartTime))
				{
					if (scheduleService.Instance.ExpectedStartTime.Day == DateTime.Now.Day) //same day
					{

						if (
							scheduleService.Instance.ExpectedStartTime <= DateTime.Now &&
							(scheduleService.Rule.MaxDeviationAfter == TimeSpan.Zero || scheduleService.RequestedTime.Add(scheduleService.Rule.MaxDeviationAfter) >= DateTime.Now) &&
							scheduleService.Instance.LegacyInstance.State == Legacy.ServiceState.Uninitialized
						)
						{
							instancesShouldRun.Add(scheduleService);
						}
					}
				}
				if (instancesShouldRun.Count > 0)
				{
					var shouldRun = instancesShouldRun.OrderBy(s => s.Instance.ExpectedStartTime);
					foreach (var instance in shouldRun)
					{
						int countedServicesWithSameConfiguration = _schedulingRequests.Count(s => instance.Configuration.BaseConfiguration.Name == s.Configuration.Name &&
																   s.Instance.LegacyInstance.State == Legacy.ServiceState.Running &&
																   s.Instance.LegacyInstance.State == Legacy.ServiceState.Initializing &&
																   s.Instance.LegacyInstance.State != Legacy.ServiceState.Aborting &&
																   s.Instance.Canceled == false);
						//cant run!!!!
						if (countedServicesWithSameConfiguration >= instance.Configuration.MaxConcurrent)
							continue;
						int countedServicesWithSameProfile = _schedulingRequests.Count(s => instance.Configuration.Profile.ID == s.Configuration.Profile.ID &&
							instance.Configuration.BaseConfiguration.Name == s.Configuration.Name && //should be id but no id yet
																	s.Instance.LegacyInstance.State != Legacy.ServiceState.Uninitialized &&
																	s.Instance.LegacyInstance.State != Legacy.ServiceState.Ended &&
																   s.Instance.LegacyInstance.State != Legacy.ServiceState.Aborting &&
																	s.Instance.Canceled == false);
						//cant run!!!
						if (countedServicesWithSameProfile >= instance.Configuration.MaxConcurrentPerProfile)
							continue;
						OnTimeToRun(new ServicesToRunEventArgs() { ServicesToRun = new SchedulingRequest[] { instance } });
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
			foreach (SchedulingRequest  serviceToRun in e.ServicesToRun)
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
		}

		//==================================

		#endregion






	}

	#region eventargs classes
	public class ServicesToRunEventArgs : EventArgs
	{
		public SchedulingRequest[] ServicesToRun;
	}
	public class WillNotRunEventArgs : EventArgs
	{
		public List<SchedulingRequest> WillNotRun = new List<SchedulingRequest>();
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
	public static class SchedulingRequestCollectionExtensions
	{
		public static IEnumerable<SchedulingRequest> RemoveAll(this SchedulingRequestCollection req,
									 Func<SchedulingRequest, bool> condition)
		{
			foreach (var cur in req.Where(condition))
			{
				req.Remove(cur);
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
