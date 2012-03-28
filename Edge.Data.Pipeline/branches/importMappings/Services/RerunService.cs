using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Core.Services;
using Edge.Core.Utilities;
using Edge.Core.Scheduling;
using Edge.Core;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Configuration;

namespace Edge.Data.Pipeline.Services
{
	class RerunService : PipelineService	
	{
		protected override DateTimeRangeLimitation TargetPeriodLimitation
		{
			get { return DateTimeRangeLimitation.None; }
		}

		protected override Core.Services.ServiceOutcome DoPipelineWork()
		{
			string serviceName = Instance.Configuration.GetOption("ServiceToRun");

			using (ServiceClient<IScheduleManager> scheduleManager = new ServiceClient<IScheduleManager>())
			{
				DateTime fromDate = this.TargetPeriod.Start.ToDateTime();
				DateTime toDate = this.TargetPeriod.End.ToDateTime();
				

				
				while (fromDate<=toDate)
				{
					// {start: {base : '2009-01-01', h:0}, end: {base: '2009-01-01', h:'*'}}
					var subRange = new DateTimeRange()
					{
						Start = new DateTimeSpecification()
						{
							BaseDateTime = fromDate,
							Hour = new DateTimeTransformation() { Type = DateTimeTransformationType.Exact, Value = 0 },
							Alignment=DateTimeSpecificationAlignment.Start
						},
						End = new DateTimeSpecification()
						{
							BaseDateTime = fromDate,
							Hour = new DateTimeTransformation() { Type = DateTimeTransformationType.Max },
							Alignment = DateTimeSpecificationAlignment.End
						}
					};

					SettingsCollection options = new SettingsCollection();
					options.Add(PipelineService.ConfigurationOptionNames.TargetPeriod, subRange.ToAbsolute().ToString());
					options.Add(PipelineService.ConfigurationOptionNames.ConflictBehavior, DeliveryConflictBehavior.Ignore.ToString());
					foreach (var option in Instance.Configuration.Options)
					{
						if (!options.ContainsKey(option.Key))
							options.Add(option.Key, option.Value);
						
					}
					//run the service
					scheduleManager.Service.AddToSchedule(serviceName,this.Instance.AccountID,DateTime.Now, options);

					fromDate = fromDate.AddDays(1);

					 
					

				}

			}
			
			return Core.Services.ServiceOutcome.Success;
		}
	}
}
