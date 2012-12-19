using System;
using System.Collections.Generic;

namespace Edge.Core.Services.Scheduling
{
    /// <summary>
    /// ShedulerConfiguration - contains specific configuration for scheduler
    /// </summary>
    public class SchedulerConfiguration
    {
        // param is used to calculate average service execution time
        public int Percentile { get; set; }

        // factor to multiply execution time to calculate max execution time
        public double MaxExecutionTimeFactor { get; set; }

        // timeframe of scheduled services (schedule services in timeframe X from now)
        public TimeSpan Timeframe { get; set; }

        // interval to check for new scheduled services (every X time check for services in timeframe)
        public TimeSpan RescheduleInterval { get; set; }

        // interval for execute scheduled services (every X time check what scheduled service are on time to be executed)
        public TimeSpan ExecuteInterval { get; set; }

        // interval for checking if there are unplanned services and should reschedule
        public TimeSpan CheckUnplannedServicesInterval { get; set; }

		// interval for refreshing service executing statistics
		public TimeSpan ExecutionStatisticsRefreshInterval { get; set; }

		// default value for execution statistics time
		public TimeSpan DefaultExecutionTime { get; set; }

        // profiles configuration
        public ProfilesCollection Profiles { get; set; }

        // services configuration
        public IList<ServiceConfiguration> ServiceConfigurationList { get; set; }
    }
}
