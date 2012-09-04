﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Data.Pipeline.Services;
using Edge.Core.Data;
using Edge.Data.Pipeline.Common.Importing;

namespace Edge.Data.Pipeline.Metrics.Services
{
	public abstract class AutoMetricsProcessorServiceBase: MetricsProcessorServiceBase
	{
		public ReaderAdapter ReaderAdapter { get; private set; }

		protected override Core.Services.ServiceOutcome DoPipelineWork()
		{
			// Setup/defaults/configuration/etc.
			// ------------------------------------------

			string checksumThreshold = Configuration.Parameters.Get<T>(Consts.ConfigurationOptions.ChecksumTheshold);
			var importManagerOptions = new MetricsDeliveryManagerOptions()
			{
				SqlTransformCommand = Configuration.Parameters.Get<T>(Consts.AppSettings.SqlTransformCommand),
				SqlStageCommand = Configuration.Parameters.Get<T>(Consts.AppSettings.SqlStageCommand),
				SqlRollbackCommand = Configuration.Parameters.Get<T>(Consts.AppSettings.SqlRollbackCommand),
				ChecksumThreshold = checksumThreshold == null ? 0.01 : double.Parse(checksumThreshold)
			};

			string fileName;
			if (!this.Configuration.Options.TryGetValue(Const.DeliveryServiceConfigurationOptions.DeliveryFileName, out fileName))
				throw new ConfigurationException(String.Format("{0} is missing in the service configuration options.", Const.DeliveryServiceConfigurationOptions.DeliveryFileName));

			DeliveryFile file = this.Delivery.Files[fileName];
			if (file == null)
				throw new Exception(String.Format("Could not find delivery file '{0}' in the delivery.", fileName));

			FileCompression compression;
			string compressionOption;
			if (this.Configuration.Options.TryGetValue(Const.DeliveryServiceConfigurationOptions.Compression, out compressionOption))
			{
				if (!Enum.TryParse<FileCompression>(compressionOption, out compression))
					throw new ConfigurationException(String.Format("Invalid compression type '{0}'.", compressionOption));
			}
			else
				compression = FileCompression.None;

			// Create format processor from configuration
			string adapterTypeName = Configuration.GetOption(Consts.ConfigurationOptions.ReaderAdapterType);
			Type readerAdapterType = Type.GetType(adapterTypeName, true);
			this.ReaderAdapter = (ReaderAdapter)Activator.CreateInstance(readerAdapterType);

			this.Mappings.OnFieldRequired = this.ReaderAdapter.GetField;

			LoadConfiguration();

			// ------------------------------------------

			using (var stream = file.OpenContents(compression: compression))
			{
				using (this.ReaderAdapter)
				{
					this.ReaderAdapter.Init(stream, Configuration);

					using (this.ImportManager = CreateImportManager(InstanceID, importManagerOptions))
					{
						this.ImportManager.BeginImport(this.Delivery);
                        bool readSuccess = false;
                        while (this.ReaderAdapter.Reader.Read())
                        {
                            readSuccess = true;
                            OnRead();
                        }

                        if (!readSuccess)
                            Edge.Core.Utilities.Log("Could Not read data from file!, check file mapping and configuration", Core.Utilities.LogMessageType.Warning);
							
						this.ImportManager.EndImport();
					}
				}
			}

			return Core.Services.ServiceOutcome.Success;
		}

		protected virtual void LoadConfiguration() { }
		protected abstract MetricsDeliveryManager CreateImportManager(long serviceInstanceID, MetricsDeliveryManagerOptions options);
		protected abstract void OnRead();

	}
}
