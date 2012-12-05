using Edge.Core.Scheduling;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
        public TimeSpan SamplingInterval { get; set; }

        // interval for service rescheduling (every X time recalculate services scheduling)
        public TimeSpan ResheduleInterval { get; set; }

        // interval for refreshing service executign statistics
        public TimeSpan ExecutionStatisticsRefreshInterval { get; set; }

        // profiles configuration
        public ProfilesCollection Profiles { get; set; }

        // services configuration
        public IList<ServiceConfiguration> ServiceConfigurationList { get; set; }
    }
}
