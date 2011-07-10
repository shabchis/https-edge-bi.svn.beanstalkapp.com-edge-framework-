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
			AdMetricsImportManager importManager = new AdMetricsImportManager(this.Instance.InstanceID);

			DeliveryRollbackOperation rollback = this.HandleConflicts(importManager, DeliveryConflictBehavior.Rollback);
			if (rollback != null)
				rollback.Wait();

			importManager.Commit(new Delivery[] { this.Delivery });

			return ServiceOutcome.Success;
		}
	}
}
