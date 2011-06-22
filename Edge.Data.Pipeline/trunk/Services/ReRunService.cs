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

namespace Edge.Data.Pipeline.Services
{
	class ReRunService : PipelineService	
	{
		protected override Core.Services.ServiceOutcome DoPipelineWork()
		{

			using (ServiceClient<IScheduleManager> scheduleManager = new ServiceClient<IScheduleManager>())
			{
				string targetPeriod = this.TargetPeriod.ToString();
				//Parso json
				JObject jObjecttimeRange = JObject.Parse(targetPeriod);
				DateTime fromDate = (DateTime)JsonConvert.DeserializeObject<DateTime>(jObjecttimeRange["start"].ToString());
				DateTime toDate = (DateTime)JsonConvert.DeserializeObject<DateTime>(jObjecttimeRange["end"].ToString()); 
				string serviceName=Instance.Configuration.Options["ServiceToRun"];
				
				while (fromDate<=toDate)
				{
					// {start: {exact : '2009-01-01', h:0}, end: {'2009-01-01', h:'*'}}
					var subRange = new DateTimeRange()
					{
						Start = new DateTimeSpecification()
						{
							ExactDateTime = fromDate,
							Hour = new DateTimeTransformation() { Type = DateTimeTransformationType.Exact, Value = 0 }
						},
						End = new DateTimeSpecification()
						{
							ExactDateTime = fromDate,
							Hour = new DateTimeTransformation() { Type = DateTimeTransformationType.Max }
						}
					};

					// { start: '2009-01-01 00:00:00.00000', end: '2009-01-01 23:59:59.99999' }
					var finalRange = new DateTimeRange()
					{
						Start = new DateTimeSpecification() { ExactDateTime = subRange.Start.ToDateTime() },
						End = new DateTimeSpecification() { ExactDateTime = subRange.End.ToDateTime() }
					};

					SettingsCollection options = new SettingsCollection();
					options.Add("TargetPeriod", finalRange.ToString());

					//run the service
					scheduleManager.Service.AddToSchedule(serviceName,this.Delivery.Account.ID,DateTime.Now, options);

					fromDate = fromDate.AddDays(1);

					 
					

				}

			}
			
			return Core.Services.ServiceOutcome.Success;
		}
	}
}
