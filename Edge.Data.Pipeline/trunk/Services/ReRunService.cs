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
				string serviceName=this.Delivery.Parameters["ServiceToRun"].ToString();
				
				while (fromDate<=toDate)
				{

					//run the service
					scheduleManager.Service.AddToSchedule(serviceName,this.Delivery.Account.ID,DateTime.Now,new SettingsCollection(String.Format("TargetPeriod: {0}", DayCode.ToDayCode(fromDate))));

					fromDate = fromDate.AddDays(1);


					

				}

			}
			
			return Core.Services.ServiceOutcome.Success;
		}
	}
}
