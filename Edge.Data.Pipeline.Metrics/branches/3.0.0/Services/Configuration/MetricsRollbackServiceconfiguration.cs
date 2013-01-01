using System;
using System.Runtime.Serialization;
using Edge.Core.Services;
using Edge.Data.Pipeline.Services;

namespace Edge.Data.Pipeline.Metrics.Services.Configuration
{
	/// <summary>
	/// Additional configuration for MetricsRollbackService
	/// </summary>
	[Serializable]
	public class MetricsRollbackServiceconfiguration : PipelineServiceConfiguration
	{
		#region Properties
		private string _deliveries;
		public string Deliveries { get { return _deliveries; } set { EnsureUnlocked(); _deliveries = value; } }

		private string _outputs;
		public string Outputs { get { return _outputs; } set { EnsureUnlocked(); _outputs = value; } }

		private string _tableName;
		public string TableName { get { return _tableName; } set { EnsureUnlocked(); _tableName = value; } }
		
		private string _spRollbackDeliveries;
		public string RollbackDeliveriesStoredProc { get { return _spRollbackDeliveries; } set { EnsureUnlocked(); _spRollbackDeliveries = value; } }
		
		private string _spRollbackOutputs;
		public string RollbackOutputsStoredProc { get { return _spRollbackOutputs; } set { EnsureUnlocked(); _spRollbackOutputs = value; } }
		#endregion

		#region Override Methods
		protected override void Serialize(SerializationInfo info, StreamingContext context)
		{
			base.Serialize(info, context);

			info.AddValue("Deliveries", _deliveries);
			info.AddValue("Outputs", _outputs);
			info.AddValue("TableName", _tableName);
			info.AddValue("RollbackDeliveriesStoredProc", _spRollbackDeliveries);
			info.AddValue("RollbackOutputsStoredProc", _spRollbackOutputs);
		}

		protected override void Deserialize(SerializationInfo info, StreamingContext context)
		{
			base.Deserialize(info, context);

			_deliveries           = (string)info.GetValue("Deliveries", typeof(string));
			_outputs              = (string)info.GetValue("Outputs", typeof(string));
			_tableName            = (string)info.GetValue("TableName", typeof(string));
			_spRollbackDeliveries = (string)info.GetValue("RollbackDeliveriesStoredProc", typeof(string));
			_spRollbackOutputs    = (string)info.GetValue("RollbackOutputsStoredProc", typeof(string));
		}

		protected override void CopyConfigurationData(ServiceConfiguration sourceConfig, ServiceConfiguration targetConfig)
		{
			base.CopyConfigurationData(sourceConfig, targetConfig);
			if (!(targetConfig is MetricsRollbackServiceconfiguration) || !(sourceConfig is MetricsRollbackServiceconfiguration))
				return;

			var sourcec = (MetricsRollbackServiceconfiguration)sourceConfig;
			var targetc = (MetricsRollbackServiceconfiguration)targetConfig;

			// Only copy values
			targetc.Deliveries = sourcec.Deliveries;
			targetc.Outputs = sourcec.Outputs;
			targetc.TableName = sourcec.TableName;
			targetc.RollbackDeliveriesStoredProc = sourcec.RollbackDeliveriesStoredProc;
			targetc.RollbackOutputsStoredProc = sourcec.RollbackOutputsStoredProc;
		}
		#endregion
	}
}
