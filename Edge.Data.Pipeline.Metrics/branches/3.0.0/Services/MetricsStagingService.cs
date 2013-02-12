using System;
using Edge.Core.Services;
using Edge.Data.Pipeline.Metrics.Managers;
using Edge.Data.Pipeline.Metrics.Misc;
using Edge.Data.Pipeline.Services;
using Edge.Core.Utilities;

namespace Edge.Data.Pipeline.Metrics.Services
{
	public class MetricsStagingService : PipelineService
	{
		#region Override DoWork
		protected override ServiceOutcome DoPipelineWork()
		{
			var checksumThreshold = Configuration.Parameters.Get<string>(Consts.ConfigurationOptions.ChecksumTheshold, false);
			var options = new MetricsDeliveryManagerOptions
				{
					SqlStageCommand = Configuration.Parameters.Get<string>(Consts.AppSettings.SqlStageCommand),
					SqlRollbackCommand = Configuration.Parameters.Get<string>(Consts.AppSettings.SqlRollbackCommand),
					ChecksumThreshold = checksumThreshold == null ? 0.01 : double.Parse(checksumThreshold)
				};

			using (var importManager = new MetricsDeliveryManager(InstanceID, options: options))
			{
				var success = false;
				do
				{
					try
					{
						// perform staging
						Log("Staging: start", LogMessageType.Information);
						importManager.Stage(new[] {Delivery});
						Log("Staging: end", LogMessageType.Information);
						success = true;
					}
					catch (DeliveryConflictException dceex)
					{
						// rollback in case of exception
						Log("Rollback: start", LogMessageType.Information);
						importManager.RollbackOutputs(dceex.ConflictingOutputs);
						Log("Rollback: end", LogMessageType.Information);
					}
					catch (Exception ex)
					{
						throw new Exception(String.Format("Delivery {0} failed during staging.", Delivery.DeliveryID), ex);
					}
				} 
				while (!success);
			}
			return ServiceOutcome.Success;
		} 
		#endregion
	}
}
