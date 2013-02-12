using System;
using Edge.Core.Services;
using Edge.Core.Utilities;
using Edge.Data.Pipeline.Metrics.Managers;
using Edge.Data.Pipeline.Metrics.Misc;
using Edge.Data.Pipeline.Services;

namespace Edge.Data.Pipeline.Metrics.Services
{
	public class MetricsTransformService : PipelineService
	{
		protected override ServiceOutcome DoPipelineWork()
		{
			var checksumThreshold = Configuration.Parameters.Get<string>(Consts.ConfigurationOptions.ChecksumTheshold, false);
			var options = new MetricsDeliveryManagerOptions
			{
				SqlTransformCommand = Configuration.Parameters.Get<string>(Consts.AppSettings.SqlTransformCommand),
				ChecksumThreshold = checksumThreshold == null ? 0.01 : double.Parse(checksumThreshold)
			};

			using (var importManager = new MetricsDeliveryManager(InstanceID, options: options))
			{
				// TODO: need this? Only check tickets, don't check conflicts
				HandleConflicts(importManager, DeliveryConflictBehavior.Ignore, getBehaviorFromConfiguration: false);

				try
				{
					// perform transform
					Log("Transform: start", LogMessageType.Information);
					importManager.Transform(new[] {Delivery});
					Log("Transform: end", LogMessageType.Information);
				}
				catch (Exception ex)
				{
					throw new Exception(String.Format("Delivery {0} failed during Transform.", Delivery.DeliveryID), ex);
				}
			}
			return ServiceOutcome.Success;
		}
	}
}
