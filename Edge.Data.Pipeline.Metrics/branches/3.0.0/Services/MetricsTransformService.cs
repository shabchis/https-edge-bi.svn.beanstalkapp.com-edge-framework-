using System;
using System.Collections.Generic;
using Edge.Core.Services;
using Edge.Core.Utilities;
using Edge.Data.Objects;
using Edge.Data.Pipeline.Metrics.Managers;
using Edge.Data.Pipeline.Metrics.Misc;
using Edge.Data.Pipeline.Services;

namespace Edge.Data.Pipeline.Metrics.Services
{
	public class MetricsTransformService : PipelineService
	{
		#region Data Members
		private int _accountId = -1;
		#endregion

		#region DoPipelineWork
		protected override ServiceOutcome DoPipelineWork()
		{
			if (Configuration.Parameters["AccountID"] != null)
			{
				int.TryParse(Configuration.Parameters["AccountID"].ToString(), out _accountId);
			}

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

				// perform transform
				Log(String.Format("Start transform deliver '{0}'", Delivery.DeliveryID), LogMessageType.Information);
				importManager.Transform(new[] { Delivery });
				Log(String.Format("Finished transform deliver '{0}'", Delivery.DeliveryID), LogMessageType.Information);
			}
			return ServiceOutcome.Success;
		} 
		#endregion
	}
}
