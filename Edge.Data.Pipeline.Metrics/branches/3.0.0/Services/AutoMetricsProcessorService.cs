using System;
using System.Configuration;
using System.Linq;
using Edge.Core.Services;
using Edge.Data.Objects;
using Edge.Data.Pipeline.Mapping;
using Edge.Data.Pipeline.Metrics.Managers;
using Edge.Data.Pipeline.Metrics.Misc;
using Edge.Data.Pipeline.Metrics.Services.Configuration;
using Edge.Data.Pipeline.Objects;

namespace Edge.Data.Pipeline.Metrics.Services
{
	/// <summary>
	/// Service for automatic data processing
	/// </summary>
	public class AutoMetricsProcessorService: MetricsProcessorServiceBase
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
				using (ImportManager = new MetricsDeliveryManager(InstanceID, EdgeTypes, _importManagerOptions))
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
							ProcessMetrics();
						}

						if (!readSuccess)
							Log("Could Not read data from file!, check file mapping and configuration", Core.Utilities.LogMessageType.Warning);

						ImportManager.EndImport();
					}
				}
			}
			return ServiceOutcome.Success;
		}

		protected MetricsUnit GetSampleMetrics()
		{
			try
			{
				// load sample file, read only one row in order to create metrics table by sample metric unit
				ReaderAdapter.Init(FileManager.Open(Configuration.SampleFilePath, compression: _compression), Configuration);
				ReaderAdapter.Reader.Read();

				CurrentMetricsUnit = new MetricsUnit {GetEdgeField = GetEdgeField};
				MetricsMappings.Apply(CurrentMetricsUnit);	
				return CurrentMetricsUnit;
			}
			catch (Exception ex)
			{
				throw new ConfigurationErrorsException(String.Format("Failed to create metrics by sample file '{0}', ex: {1}", Configuration.SampleFilePath, ex.Message));
			}
		} 
		
		protected void ProcessMetrics()
		{
			// fill the metrics using mapping
			CurrentMetricsUnit = new MetricsUnit {GetEdgeField = GetEdgeField };
			MetricsMappings.Apply(CurrentMetricsUnit);

			var signature = new Signature();
			SignatureMappings.Apply(signature);

			// check if signature is already exists in delivery outputs
			var outputs = from output in Delivery.Outputs
						  where output.Signature.Equals(signature.ToString())
						  select output;

			// attach output to Metrics: take existing or create new
			var op = outputs.FirstOrDefault();
			if (op != null)
				CurrentMetricsUnit.Output = op;
			else
			{
				var deliveryOutput = new DeliveryOutput
				{
					Signature = signature.Value,
					TimePeriodStart = CurrentMetricsUnit.TimePeriodStart,
					TimePeriodEnd = CurrentMetricsUnit.TimePeriodEnd,
					Account = CurrentMetricsUnit.Account,
					Channel = CurrentMetricsUnit.Channel
				};
				Delivery.Outputs.Add(deliveryOutput);
				CurrentMetricsUnit.Output = deliveryOutput;
			}

			// import metrics into DB
			ImportManager.ImportMetrics(CurrentMetricsUnit);
		}
		#endregion

		#region Configuration
		protected void LoadConfiguration()
		{
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
			Mappings.OnMappingApplied = SetEdgeType;

			if (!Mappings.Objects.TryGetValue(typeof(MetricsUnit), out MetricsMappings))
				throw new MappingConfigurationException("Missing mapping definition for GenericMetricsUnit.", "Object");

			if (!Mappings.Objects.TryGetValue(typeof(Signature), out SignatureMappings))
				throw new MappingConfigurationException("Missing mapping definition for Signature.", "Object");
		}

		#endregion
	}
}
