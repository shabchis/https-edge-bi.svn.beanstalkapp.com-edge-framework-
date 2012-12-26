using System;
using Edge.Core.Services;

namespace Edge.Data.Pipeline.Services
{
	public class RerunService : PipelineService
	{
		protected override ServiceOutcome DoPipelineWork()
		{
			//var serviceName = Configuration.Parameters.Get<string>("ServiceToRun");

			if (Configuration.TimePeriod != null)
			{
				DateTime fromDate = Configuration.TimePeriod.Value.Start.ToDateTime();
				DateTime toDate = Configuration.TimePeriod.Value.End.ToDateTime();

				while (fromDate <= toDate)
				{
					// {start: {base : '2009-01-01', h:0}, end: {base: '2009-01-01', h:'*'}}
					var subRange = new DateTimeRange
						{
							Start = new DateTimeSpecification
								{
									BaseDateTime = fromDate,
									Hour = new DateTimeTransformation { Type = DateTimeTransformationType.Exact, Value = 0 },
									Alignment = DateTimeSpecificationAlignment.Start
								},
							End = new DateTimeSpecification
								{
									BaseDateTime = fromDate,
									Hour = new DateTimeTransformation { Type = DateTimeTransformationType.Max },
									Alignment = DateTimeSpecificationAlignment.End
								}
						};

					var configuration = new PipelineServiceConfiguration
						{
							TimePeriod = subRange.ToAbsolute(),
							ConflictBehavior = DeliveryConflictBehavior.Ignore
						};

					foreach (var param in Configuration.Parameters)
						configuration.Parameters[param.Key] = param.Value;

					// TODO shriat add to scheduler 
					//this.Environment.ScheduleServiceByName(serviceName, this.Configuration.Profile.ProfileID, configuration);
					var serviceInstance = Environment.NewServiceInstance(Configuration);
					Environment.AddToSchedule(serviceInstance);

					fromDate = fromDate.AddDays(1);
				}
			}

			return ServiceOutcome.Success;
		}
	}
}
