using System;
using Edge.Core.Services;
using Edge.Data.Pipeline.Metrics.Base;
using Edge.Data.Pipeline.Services;
using Edge.Core.Utilities;

namespace Edge.Data.Pipeline.Metrics.Services
{
	public class MetricsStagingService : PipelineService
	{
		#region Override DoWork
		protected override ServiceOutcome DoPipelineWork()
		{
			// ----------------
			// SETUP
			// ----------------
			var checksumThreshold = Configuration.Parameters.Get<string>(Consts.ConfigurationOptions.ChecksumTheshold, false);

			var options = new MetricsDeliveryManagerOptions
				{
					SqlTransformCommand = Configuration.Parameters.Get<string>(Consts.AppSettings.SqlTransformCommand),
					SqlStageCommand = Configuration.Parameters.Get<string>(Consts.AppSettings.SqlStageCommand),
					SqlRollbackCommand = Configuration.Parameters.Get<string>(Consts.AppSettings.SqlRollbackCommand),
					ChecksumThreshold = checksumThreshold == null ? 0.01 : double.Parse(checksumThreshold)
				};

			Type importManagerType = Configuration.Parameters.Get(Consts.ConfigurationOptions.ImportManagerType, convertFunction: raw => Type.GetType((string)raw));

			var importManager = (MetricsDeliveryManager)Activator.CreateInstance(importManagerType, InstanceID, options);
			Progress = 0.1;

			// ----------------
			// TICKETS
			// ----------------
			// Only check tickets, don't check conflicts
			HandleConflicts(importManager, DeliveryConflictBehavior.Ignore, getBehaviorFromConfiguration: false);
			Progress = 0.2;

			// ----------------
			// TRANSFORM
			// ----------------
			try
			{
				Log("Transform: start", LogMessageType.Information);
				importManager.Transform(new[] { Delivery });
				Log("Transform: end", LogMessageType.Information);
			}
			catch (Exception ex)
			{
				throw new Exception(String.Format("Delivery {0} failed during Transform.", Delivery.DeliveryID), ex);
			}

			Progress = 0.6;

			// ----------------
			// COMMIT
			// ----------------
			bool success = false;
			do
			{
				try
				{
					Log("Staging: start", LogMessageType.Information);
					importManager.Stage(new[] { Delivery });
					Log("Staging: end", LogMessageType.Information);
					success = true;
				}
				catch (DeliveryConflictException dceex)
				{
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

			return ServiceOutcome.Success;
		} 
		#endregion
	}
}
