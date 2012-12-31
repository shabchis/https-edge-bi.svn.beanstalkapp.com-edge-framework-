using System;
using System.Configuration;
using Edge.Data.Pipeline.Metrics.Base;
using Edge.Data.Pipeline.Services;

namespace Edge.Data.Pipeline.Metrics.Services
{
	public abstract class AutoMetricsProcessorServiceBase: MetricsProcessorServiceBase
	{
		public ReaderAdapter ReaderAdapter { get; private set; }

		protected override Core.Services.ServiceOutcome DoPipelineWork()
		{
			// Setup/defaults/configuration/etc.
			// ------------------------------------------

			var checksumThreshold = Configuration.Parameters.Get<string>(Consts.ConfigurationOptions.ChecksumTheshold);
			var importManagerOptions = new MetricsDeliveryManagerOptions
				{
				SqlTransformCommand = Configuration.Parameters.Get<string>(Consts.AppSettings.SqlTransformCommand),
				SqlStageCommand = Configuration.Parameters.Get<string>(Consts.AppSettings.SqlStageCommand),
				SqlRollbackCommand = Configuration.Parameters.Get<string>(Consts.AppSettings.SqlRollbackCommand),
				ChecksumThreshold = checksumThreshold == null ? 0.01 : double.Parse(checksumThreshold)
			};

			string fileName;
			if (!Configuration.Options.TryGetValue(Const.DeliveryServiceConfigurationOptions.DeliveryFileName, out fileName))
				throw new ConfigurationErrorsException(String.Format("{0} is missing in the service configuration options.", Const.DeliveryServiceConfigurationOptions.DeliveryFileName));

			DeliveryFile file = Delivery.Files[fileName];
			if (file == null)
				throw new Exception(String.Format("Could not find delivery file '{0}' in the delivery.", fileName));

			FileCompression compression;
			string compressionOption;
			if (this.Configuration.Options.TryGetValue(Const.DeliveryServiceConfigurationOptions.Compression, out compressionOption))
			{
				if (!Enum.TryParse(compressionOption, out compression))
					throw new ConfigurationErrorsException(String.Format("Invalid compression type '{0}'.", compressionOption));
			}
			else
				compression = FileCompression.None;

			// Create format processor from configuration
			string adapterTypeName = Configuration.GetOption(Consts.ConfigurationOptions.ReaderAdapterType);
			Type readerAdapterType = Type.GetType(adapterTypeName, true);
			ReaderAdapter = (ReaderAdapter)Activator.CreateInstance(readerAdapterType);

			Mappings.OnFieldRequired = ReaderAdapter.GetField;

			LoadConfiguration();

			// ------------------------------------------

			using (var stream = file.OpenContents(compression: compression))
			{
				using (ReaderAdapter)
				{
					ReaderAdapter.Init(stream, Configuration);

					using (ImportManager = CreateImportManager(InstanceID, importManagerOptions))
					{
						ImportManager.BeginImport(Delivery);
                        bool readSuccess = false;
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

			return Core.Services.ServiceOutcome.Success;
		}

		protected virtual void LoadConfiguration() { }
		protected abstract MetricsDeliveryManager CreateImportManager(Guid serviceInstanceID, MetricsDeliveryManagerOptions options);
		protected abstract void OnRead();

	}
}
