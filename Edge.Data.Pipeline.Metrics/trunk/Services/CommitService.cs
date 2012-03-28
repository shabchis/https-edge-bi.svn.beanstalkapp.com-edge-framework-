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
	public class CommitService : PipelineService
	{
		protected override ServiceOutcome DoPipelineWork()
		{
			// ----------------
			// SETUP

			string checksumThreshold = Instance.Configuration.Options[Consts.ConfigurationOptions.ChecksumTheshold];
			
			MetricsImportManagerOptions options = new MetricsImportManagerOptions()
			{
				SqlPrepareCommand = Instance.Configuration.Options[Consts.AppSettings.SqlPrepareCommand],
				SqlCommitCommand = Instance.Configuration.Options[Consts.AppSettings.SqlCommitCommand],
				SqlRollbackCommand = Instance.Configuration.Options[Consts.AppSettings.SqlRollbackCommand],
				ChecksumThreshold = checksumThreshold == null ? 0.01 : double.Parse(checksumThreshold)
			};

			string importManagerTypeName = Instance.Configuration.GetOption(Consts.ConfigurationOptions.ImportManagerType);
			Type importManagerType = Type.GetType(importManagerTypeName);

			var importManager = (MetricsImportManager) Activator.CreateInstance(importManagerType, this.Instance.InstanceID, options);
			ReportProgress(0.1);

			// ----------------
			// TICKETS

			// Only check tickets, don't check conflicts
			this.HandleConflicts(importManager, DeliveryConflictBehavior.Ignore, getBehaviorFromConfiguration: false);
			ReportProgress(0.2);

			// ----------------
			// PREPARE
			try
			{
				Log.Write("Prepare: start", LogMessageType.Information);
				importManager.Prepare(new Delivery[] { this.Delivery });
				Log.Write("Prepare: end", LogMessageType.Information);
			}
			catch (Exception ex)
			{
				throw new Exception(String.Format("Delivery {0} failed during prepare.", this.Delivery.DeliveryID), ex);
			}

			ReportProgress(0.6);

			// ----------------
			// COMMIT
			bool success = false;
			do
			{
				try
				{
					Log.Write("Commit: start", LogMessageType.Information);
					importManager.Commit(new Delivery[] { this.Delivery });
					Log.Write("Commit: end", LogMessageType.Information);
					success = true;
				}
				catch (DeliveryConflictException dceex)
				{
					Log.Write("Rollback: start", LogMessageType.Information);
					importManager.Rollback(dceex.ConflictingDeliveries);
					Log.Write("Rollback: end", LogMessageType.Information);
				}
				catch (Exception ex)
				{
					throw new Exception(String.Format("Delivery {0} failed during commit.", this.Delivery.DeliveryID), ex);
				}
			}
			while (!success);

			return ServiceOutcome.Success;
		}
	}
}
