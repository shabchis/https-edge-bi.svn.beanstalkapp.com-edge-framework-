using System;
using System.Runtime.Serialization;
using Edge.Core.Services;

namespace Edge.Data.Pipeline.Services
{
	[Serializable]
	public class PipelineServiceConfiguration : ServiceConfiguration
	{
		#region Properties
		Guid? _deliveryID;
		public Guid? DeliveryID { get { return _deliveryID; } set { EnsureUnlocked(); _deliveryID = value; } }

		DateTimeRange? _timePeriod;
		public DateTimeRange? TimePeriod { get { return _timePeriod; } set { EnsureUnlocked(); _timePeriod = value; } }

		DeliveryConflictBehavior? _conflictBehavior;
		public DeliveryConflictBehavior? ConflictBehavior { get { return _conflictBehavior; } set { EnsureUnlocked(); _conflictBehavior = value; } }

		private string _mappingConfigPath;
		public string MappingConfigPath { get { return _mappingConfigPath; } set { EnsureUnlocked(); _mappingConfigPath = value; } }

		#endregion

		#region Ctors
		public PipelineServiceConfiguration() {}

		protected PipelineServiceConfiguration(SerializationInfo info, StreamingContext context)
			: base(info, context){} 
		#endregion

		#region Overrides
		protected override void Serialize(SerializationInfo info, StreamingContext context)
		{
			base.Serialize(info, context);
			info.AddValue("_deliveryID", _deliveryID);
			info.AddValue("_timePeriod", _timePeriod.ToString());
			info.AddValue("_conflictBehavior", _conflictBehavior);
			info.AddValue("_mappingConfigPath", _mappingConfigPath);
		}

		protected override void Deserialize(SerializationInfo info, StreamingContext context)
		{
			base.Deserialize(info, context);
			_deliveryID = (Guid?)info.GetValue("_deliveryID", typeof(Guid?));
			_conflictBehavior = (DeliveryConflictBehavior?)info.GetValue("_conflictBehavior", typeof(DeliveryConflictBehavior?));
			_mappingConfigPath = (string)info.GetValue("_mappingConfigPath", typeof(string));
			var timeStr = (string)info.GetValue("_timePeriod", typeof(string));
			if (!String.IsNullOrEmpty(timeStr))
				_timePeriod = DateTimeRange.Parse(timeStr); //(DateTimeRange?)info.GetValue("_timePeriod", typeof(DateTimeRange?));
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
			targetc._mappingConfigPath = sourcec._mappingConfigPath ?? sourcec._mappingConfigPath;
		} 
		#endregion
	}
}
