using System;
using System.Runtime.Serialization;
using Edge.Core.Services;
using Edge.Data.Pipeline.Services;

namespace Edge.Data.Pipeline.Metrics.Services.Configuration
{
	/// <summary>
	/// Additional configuration for AutoMetricsProcessorService
	/// </summary>
	[Serializable]
	public class AutoMetricsProcessorServiceConfiguration : PipelineServiceConfiguration
	{
		#region Properties
		private string _deliveryFileName;
		public string DeliveryFileName { get { return _deliveryFileName; } set { EnsureUnlocked(); _deliveryFileName = value; } }

		private string _compression;
		public string Compression { get { return _compression; } set { EnsureUnlocked(); _compression = value; } }

		private string _readerAdapterType;
		public string ReaderAdapterType { get { return _readerAdapterType; } set { EnsureUnlocked(); _readerAdapterType = value; } }

		private string _mappingConfigPath;
		public string MappingConfigPath { get { return _mappingConfigPath; } set { EnsureUnlocked(); _mappingConfigPath = value; } }

		#endregion

		#region Ctors
		public AutoMetricsProcessorServiceConfiguration() {}

		protected AutoMetricsProcessorServiceConfiguration(SerializationInfo info, StreamingContext context)
			: base(info, context){} 
		#endregion

		#region Override Methods
		protected override void Serialize(SerializationInfo info, StreamingContext context)
		{
			base.Serialize(info, context);
			info.AddValue("DeliveryFileName", _deliveryFileName);
			info.AddValue("Compression", _compression);
			info.AddValue("ReaderAdapterType", _readerAdapterType);
			info.AddValue("MappingConfigPath", _mappingConfigPath);
		}

		protected override void Deserialize(SerializationInfo info, StreamingContext context)
		{
			base.Deserialize(info, context);
			_deliveryFileName = (string)info.GetValue("DeliveryFileName", typeof(string));
			_compression = (string)info.GetValue("Compression", typeof(string));
			_readerAdapterType = (string)info.GetValue("ReaderAdapterType", typeof(string));
			_mappingConfigPath = (string)info.GetValue("MappingConfigPath", typeof(string));
		}

		protected override void CopyConfigurationData(ServiceConfiguration sourceConfig, ServiceConfiguration targetConfig)
		{
			base.CopyConfigurationData(sourceConfig, targetConfig);
			if (!(targetConfig is AutoMetricsProcessorServiceConfiguration) || !(sourceConfig is AutoMetricsProcessorServiceConfiguration))
				return;

			var sourcec = (AutoMetricsProcessorServiceConfiguration)sourceConfig;
			var targetc = (AutoMetricsProcessorServiceConfiguration)targetConfig;

			// Only copy values
			targetc.DeliveryFileName = sourcec.DeliveryFileName;
			targetc.Compression = sourcec.Compression;
			targetc.ReaderAdapterType = sourcec.ReaderAdapterType;
			targetc.MappingConfigPath = sourcec.MappingConfigPath;
		} 
		#endregion
	}
}
