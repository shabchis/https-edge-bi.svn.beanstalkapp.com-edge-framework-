using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Core.Data;
using Edge.Data.Objects;
using Edge.Data.Pipeline;
using Edge.Data.Pipeline.Services;
using Edge.Data.Pipeline.Mapping;
using Edge.Data.Pipeline.Common.Importing;
using System.IO;
using System.Xml;
using Edge.Data.Pipeline.Metrics.Services;

namespace Edge.Data.Pipeline.Metrics.AdMetrics
{
	/// <summary>
	/// Base class for ad metrics processors.
	/// </summary>
	public class AutoAdMetricsProcessorService : AutoMetricsProcessorServiceBase
	{
		MappingContainer _adMappings;
		MappingContainer _metricsMappings;
		MappingContainer _signatureMappings;

		public new AdMetricsImportManager ImportManager
		{
			get { return (AdMetricsImportManager)base.ImportManager; }
		}

		protected override MetricsImportManager CreateImportManager(long serviceInstanceID, MetricsImportManagerOptions options)
		{
			return new AdMetricsImportManager(serviceInstanceID, options);
		}

		protected override void LoadConfiguration()
		{
			if (!this.Mappings.Objects.TryGetValue(typeof(Ad), out _adMappings))
				throw new MappingConfigurationException("Missing mapping definition for Ad.", "Object");

			if (!this.Mappings.Objects.TryGetValue(typeof(AdMetricsUnit), out _metricsMappings))
				throw new MappingConfigurationException("Missing mapping definition for AdMetricsUnit.", "Object");

			if (!this.Mappings.Objects.TryGetValue(typeof(Signature), out _metricsMappings))
				throw new MappingConfigurationException("Missing mapping definition for Signature.", "Object");
		}

		
		protected override void OnRead()
		{
			var ad = new Ad();
			_adMappings.Apply(ad);
			this.ImportManager.ImportAd(ad);

			var metrics = new AdMetricsUnit();
			_metricsMappings.Apply(metrics);

			var signature = new Signature();
			_signatureMappings.Apply(signature);

			//checking if signature is already exists in delivery outputs
			var outputs = from output in this.Delivery.Outputs
						  where output.Signature.Equals(signature.ToString())
						  select output;

			DeliveryOutput op = outputs.FirstOrDefault<DeliveryOutput>();
			if (op != null)
				//Attaching output to Metrics
				(metrics as AdMetricsUnit).Output = op;
			else
			{
				DeliveryOutput deliveryOutput = new DeliveryOutput() 
				{ 
					Signature = signature.ToString(), 
					TimePeriodStart =metrics.TimePeriodStart,
					TimePeriodEnd = metrics.TimePeriodEnd,
					Account = metrics.Ad.Account,
					Channel = metrics.Ad.Channel
				};
				this.Delivery.Outputs.Add(deliveryOutput);
				//Attaching output to Metrics
				(metrics as AdMetricsUnit).Output = deliveryOutput;
			}

			this.ImportManager.ImportMetrics(metrics);

		
		}
	}
}
