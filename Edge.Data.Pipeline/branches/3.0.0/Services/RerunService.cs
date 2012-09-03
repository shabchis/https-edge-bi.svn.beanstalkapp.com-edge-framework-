using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Core.Services;
using Edge.Core.Utilities;
using Edge.Core;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Configuration;

namespace Edge.Data.Pipeline.Services
{
	public class RerunService : PipelineService
	{

		protected override Core.Services.ServiceOutcome DoPipelineWork()
		{
			string serviceName = this.Configuration.Parameters.Get<string>("ServiceToRun");


			DateTime fromDate = this.Configuration.TimePeriod.Value.Start.ToDateTime();
			DateTime toDate = this.Configuration.TimePeriod.Value.End.ToDateTime();

			while (fromDate <= toDate)
			{
				// {start: {base : '2009-01-01', h:0}, end: {base: '2009-01-01', h:'*'}}
				var subRange = new DateTimeRange()
				{
					Start = new DateTimeSpecification()
					{
						BaseDateTime = fromDate,
						Hour = new DateTimeTransformation() { Type = DateTimeTransformationType.Exact, Value = 0 },
						Alignment = DateTimeSpecificationAlignment.Start
					},
					End = new DateTimeSpecification()
					{
						BaseDateTime = fromDate,
						Hour = new DateTimeTransformation() { Type = DateTimeTransformationType.Max },
						Alignment = DateTimeSpecificationAlignment.End
					}
				};

				var configuration = new PipelineServiceConfiguration()
				{
					TimePeriod = subRange.ToAbsolute(),
					ConflictBehavior = DeliveryConflictBehavior.Ignore
				};

				foreach (var param in this.Configuration.Parameters)
					configuration.Parameters[param.Key] = param.Value;

				this.Environment.ScheduleServiceByName(serviceName, this.Configuration.Profile.ProfileID, configuration);

				fromDate = fromDate.AddDays(1);
			}

			return Core.Services.ServiceOutcome.Success;
		}
	}
}
