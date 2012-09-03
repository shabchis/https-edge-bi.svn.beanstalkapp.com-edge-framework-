using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Core.Services;

namespace Edge.Data.Pipeline.Services
{
	[Serializable]
	public class PipelineServiceConfiguration : ServiceConfiguration
	{
		Guid? _deliveryID;
		public Guid? DeliveryID { get { return _deliveryID; } set { EnsureUnlocked(); _deliveryID = value; } }

		DateTimeRange? _timePeriod;
		public DateTimeRange? TimePeriod { get { return _timePeriod; } set { EnsureUnlocked(); _timePeriod = value; } }

		DeliveryConflictBehavior? _conflictBehavior;
		public DeliveryConflictBehavior? ConflictBehavior { get { return _conflictBehavior; } set { EnsureUnlocked(); _conflictBehavior = value; } }

		protected override void Serialize(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
		{
			base.Serialize(info, context);
			info.AddValue("_deliveryID", _deliveryID);
			info.AddValue("_timePeriod", _timePeriod);
			info.AddValue("_conflictBehavior", _conflictBehavior);
		}

		protected override void Deserialize(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
		{
			base.Deserialize(info, context);
			_deliveryID = (Guid?)info.GetValue("_deliveryID", typeof(Guid?));
			_timePeriod = (DateTimeRange?)info.GetValue("_timePeriod", typeof(DateTimeRange?));
			_conflictBehavior = (DeliveryConflictBehavior?)info.GetValue("_conflictBehavior", typeof(DeliveryConflictBehavior?));
		}

		protected override void CopyConfigurationData(ServiceConfiguration sourceConfig, ServiceConfiguration targetConfig)
		{
			base.CopyConfigurationData(sourceConfig, targetConfig);
			if (!(targetConfig is PipelineServiceConfiguration) || !(sourceConfig is PipelineServiceConfiguration))
				return;

			var sourcec = (PipelineServiceConfiguration)sourceConfig;
			var targetc = (PipelineServiceConfiguration)targetConfig;

			// Only copy values
			targetc._deliveryID = sourcec._deliveryID ?? targetc._deliveryID;
			targetc._timePeriod = sourcec._timePeriod ?? targetc._timePeriod;
			targetc._conflictBehavior = sourcec._conflictBehavior ?? targetc._conflictBehavior;
		}
	}
}
