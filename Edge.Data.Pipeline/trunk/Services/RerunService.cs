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

namespace Edge.Data.Pipeline.Services
{
	class RerunService : PipelineService	
	{
		protected override Core.Services.ServiceOutcome DoPipelineWork()
		{

			using (ServiceClient<IScheduleManager> scheduleManager = new ServiceClient<IScheduleManager>())
			{
				DateTime fromDate = this.TargetPeriod.Start.ToDateTime();
				DateTime toDate = this.TargetPeriod.End.ToDateTime();
				string serviceName=Instance.Configuration.Options["ServiceToRun"];
				
				while (fromDate<=toDate)
				{
					// {start: {base : '2009-01-01', h:0}, end: {base: '2009-01-01', h:'*'}}
					var subRange = new DateTimeRange()
					{
						Start = new DateTimeSpecification()
						{
							BaseDateTime = fromDate,
							Hour = new DateTimeTransformation() { Type = DateTimeTransformationType.Exact, Value = 0 },
							Boundary=DateTimeSpecificationBounds.Lower
						},
						End = new DateTimeSpecification()
						{
							BaseDateTime = fromDate,
							Hour = new DateTimeTransformation() { Type = DateTimeTransformationType.Max },
							Boundary = DateTimeSpecificationBounds.Upper
						}
					};

					// { start: '2009-01-01 00:00:00.00000', end: '2009-01-01 23:59:59.99999' }
					var finalRange = new DateTimeRange()
					{
						Start = new DateTimeSpecification() { BaseDateTime = subRange.Start.ToDateTime() },
						End = new DateTimeSpecification() { BaseDateTime = subRange.End.ToDateTime() }
					};

					SettingsCollection options = new SettingsCollection();
					options.Add(PipelineService.ConfigurationOptionNames.TargetPeriod, finalRange.ToString());

					//run the service
					scheduleManager.Service.AddToSchedule(serviceName,this.Instance.AccountID,DateTime.Now, options);

					fromDate = fromDate.AddDays(1);

					 
					

				}

			}
			
			return Core.Services.ServiceOutcome.Success;
		}
	}
}
