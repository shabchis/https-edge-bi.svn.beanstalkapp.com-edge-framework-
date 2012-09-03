﻿using System;
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
		
		protected override Core.Services.ServiceOutcome DoPipelineWork()
		{
			string serviceName = Configuration.GetParameter("ServiceToRun").ToString();

			using (ServiceClient<IScheduleManager> scheduleManager = new ServiceClient<IScheduleManager>())
			{
				DateTime fromDate = this.Configuration.TimePeriod.Value.Start.ToDateTime();
				DateTime toDate = this.Configuration.TimePeriod.Value.End.ToDateTime();
				

				
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
					options.Add(PipelineService.ConfigurationOptionNames.TimePeriod, subRange.ToAbsolute().ToString());
					options.Add(PipelineService.ConfigurationOptionNames.ConflictBehavior, DeliveryConflictBehavior.Ignore.ToString());
					foreach (var option in this.Configuration.Parameters)
					{
						if (!options.ContainsKey(option.Key))
							options.Add(option.Key, option.Value);
						
					}
					//run the service
					scheduleManager.Service.AddToSchedule(serviceName,this.Configuration.Profile.Parameters["AccountID"],DateTime.Now, options);

					fromDate = fromDate.AddDays(1);

					 
					

				}

			}
			
			return Core.Services.ServiceOutcome.Success;
		}
	}
}
