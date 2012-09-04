using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Core.Services;
using Edge.Data.Pipeline;
using Edge.Data.Pipeline.Common.Importing;
using Edge.Data.Pipeline.Services;
using Edge.Core.Utilities;

namespace Edge.Data.Pipeline.Metrics.Services
{
	public class MetricsStagingService : PipelineService
	{
		protected override ServiceOutcome DoPipelineWork()
		{
			// ----------------
			// SETUP

			string checksumThreshold = Configuration.Parameters.Get<string>(Consts.ConfigurationOptions.ChecksumTheshold, false);
			
			MetricsDeliveryManagerOptions options = new MetricsDeliveryManagerOptions()
			{
				SqlTransformCommand = Configuration.Parameters.Get<string>(Consts.AppSettings.SqlTransformCommand),
				SqlStageCommand = Configuration.Parameters.Get<string>(Consts.AppSettings.SqlStageCommand),
				SqlRollbackCommand = Configuration.Parameters.Get<string>(Consts.AppSettings.SqlRollbackCommand),
				ChecksumThreshold = checksumThreshold == null ? 0.01 : double.Parse(checksumThreshold)
			};

			Type importManagerType = Configuration.Parameters.Get<Type>(Consts.ConfigurationOptions.ImportManagerType, convertFunction: raw => Type.GetType((string)raw));

			var importManager = (MetricsDeliveryManager) Activator.CreateInstance(importManagerType, this.InstanceID, options);
			Progress = 0.1;

			// ----------------
			// TICKETS

			// Only check tickets, don't check conflicts
			this.HandleConflicts(importManager, DeliveryConflictBehavior.Ignore, getBehaviorFromConfiguration: false);
			Progress = 0.2;

			// ----------------
			// TRANSFORM
			try
			{
				Log("Transform: start", LogMessageType.Information);
				importManager.Transform(new Delivery[] { this.Delivery });
				Log("Transform: end", LogMessageType.Information);
			}
			catch (Exception ex)
			{
				throw new Exception(String.Format("Delivery {0} failed during Transform.", this.Delivery.DeliveryID), ex);
			}

			Progress = 0.6;

			// ----------------
			// COMMIT
			bool success = false;
			do
			{
				try
				{
					Log("Staging: start", LogMessageType.Information);
					importManager.Stage(new Delivery[] { this.Delivery });
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
					throw new Exception(String.Format("Delivery {0} failed during staging.", this.Delivery.DeliveryID), ex);
				}
			}
			while (!success);

			return ServiceOutcome.Success;
		}
	}
}
