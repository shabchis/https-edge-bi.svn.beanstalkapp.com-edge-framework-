using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Core.Services;
using Edge.Data.Pipeline;
using Edge.Data.Pipeline.Services;

namespace Edge.Data.Pipeline.AdMetrics
{
	public class AdMetricsCommitService: PipelineService
	{
		protected override ServiceOutcome DoPipelineWork()
		{
			AdMetricsImportManager importManager = new AdMetricsImportManager(this.Instance.InstanceID, new AdMetricsImportManager.ImportManagerOptions()
			{
				SqlPrepareCommand = Instance.Configuration.Options[AdMetricsImportManager.Consts.AppSettings.SqlPrepareCommand],
				SqlCommitCommand = Instance.Configuration.Options[AdMetricsImportManager.Consts.AppSettings.SqlCommitCommand],
				SqlRollbackCommand = Instance.Configuration.Options[AdMetricsImportManager.Consts.AppSettings.SqlRollbackCommand],
			});

			DeliveryRollbackOperation rollback = this.HandleConflicts(importManager, DeliveryConflictBehavior.Rollback,
				getBehaviorFromConfiguration:false
			);

			if (rollback != null)
				rollback.Wait();

			importManager.Commit(new Delivery[] { this.Delivery });

			return ServiceOutcome.Success;
		}
	}
}
