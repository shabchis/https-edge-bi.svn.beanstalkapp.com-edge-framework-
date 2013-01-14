using System;
using System.Configuration;
using Edge.Core.Services;
using Edge.Data.Objects;
using Edge.Data.Pipeline.Mapping;
using Edge.Data.Pipeline.Metrics.Base;
using Edge.Data.Pipeline.Metrics.Services.Configuration;
using Edge.Data.Pipeline.Objects;

namespace Edge.Data.Pipeline.Metrics.Services
{
	/// <summary>
	/// Base class for automatic data processing
	/// </summary>
	public abstract class AutoMetricsProcessorServiceBase: MetricsProcessorServiceBase
	{
		#region Data Members
		private MetricsDeliveryManagerOptions _importManagerOptions;
		private FileCompression _compression;
		private DeliveryFile _deliveryFile;

		protected MappingContainer MetricsMappings;
		protected MappingContainer SignatureMappings;

		#endregion

		#region Properties
		public ReaderAdapter ReaderAdapter { get; private set; }
		public new AutoMetricsProcessorServiceConfiguration Configuration
		{
			get { return (AutoMetricsProcessorServiceConfiguration)base.Configuration; }
		}

		#endregion

		#region Override DoWork
		protected override ServiceOutcome DoPipelineWork()
		{
			InitMappings();

			LoadConfiguration();
			
			// Import data
			using (ReaderAdapter)
			{
				using (ImportManager = CreateImportManager(InstanceID, _importManagerOptions))
				{
					// create objects tables and metrics table according to sample metrics
					ImportManager.BeginImport(Delivery, GetSampleMetrics());

					// open delivery file
					using (var stream = _deliveryFile.OpenContents(compression: _compression))
					{
						ReaderAdapter.Init(stream, Configuration);
						
						// for each row in file read and import into metrics table
						var readSuccess = false; 
						while (ReaderAdapter.Reader.Read())
						{
							readSuccess = true;
							OnRead();
						}

						if (!readSuccess)
							Log("Could Not read data from file!, check file mapping and configuration", Core.Utilities.LogMessageType.Warning);

						ImportManager.EndImport();
					}
				}
			}
			return ServiceOutcome.Success;
		}

		private MetricsUnit GetSampleMetrics()
		{
			try
			{
				// load sample file, read only one row in order to create metrics table by sample metric unit
				ReaderAdapter.Init(FileManager.Open(location: Configuration.SampleFilePath, compression: _compression), Configuration);
				ReaderAdapter.Reader.Read();

				var sampleMetrics = CreateEmptyMetricsUnit();
				MetricsMappings.Apply(sampleMetrics);
				return sampleMetrics;
			}
			catch (Exception ex)
			{
				throw new ConfigurationErrorsException(String.Format("Failed to create metrics by sample file '{0}'", Configuration.SampleFilePath));
			}
		} 
		#endregion

		#region Configuration
		protected virtual void LoadConfiguration()
		{
			// TODO shirat - may be to add all these parameters to AutoMetricsProcessorServiceConfiguration?
			var checksumThreshold = Configuration.Parameters.Get<string>(Consts.ConfigurationOptions.ChecksumTheshold);
			_importManagerOptions = new MetricsDeliveryManagerOptions
			{
				SqlTransformCommand = Configuration.Parameters.Get<string>(Consts.AppSettings.SqlTransformCommand),
				SqlStageCommand = Configuration.Parameters.Get<string>(Consts.AppSettings.SqlStageCommand),
				SqlRollbackCommand = Configuration.Parameters.Get<string>(Consts.AppSettings.SqlRollbackCommand),
				ChecksumThreshold = checksumThreshold == null ? 0.01 : double.Parse(checksumThreshold)
			};

			_deliveryFile = Delivery.Files[Configuration.DeliveryFileName];
			if (_deliveryFile == null)
				throw new Exception(String.Format("Could not find delivery file '{0}' in the delivery.", Configuration.DeliveryFileName));

			if (!Enum.TryParse(Configuration.Compression, out _compression))
				throw new ConfigurationErrorsException(String.Format("Invalid compression type '{0}'.", Configuration.Compression));

			// Create format processor from configuration
			var readerAdapterType = Type.GetType(Configuration.ReaderAdapterType, true);
			ReaderAdapter = (ReaderAdapter)Activator.CreateInstance(readerAdapterType);

			Mappings.OnFieldRequired = ReaderAdapter.GetField;

			if (!Mappings.Objects.TryGetValue(typeof(GenericMetricsUnit), out MetricsMappings))
				throw new MappingConfigurationException("Missing mapping definition for GenericMetricsUnit.", "Object");

			if (!Mappings.Objects.TryGetValue(typeof(Signature), out SignatureMappings))
				throw new MappingConfigurationException("Missing mapping definition for Signature.", "Object");
		}

		#endregion

		#region Abstract Methods
		protected abstract MetricsDeliveryManager CreateImportManager(Guid serviceInstanceID, MetricsDeliveryManagerOptions options);
		protected abstract void OnRead();
		protected abstract MetricsUnit CreateEmptyMetricsUnit();
		
		#endregion
	}
}
