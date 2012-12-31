using System.Linq;
using Edge.Data.Objects;
using Edge.Data.Pipeline.Metrics.AdMetrics;
using Edge.Data.Pipeline.Metrics.Base;
using Edge.Data.Pipeline.Mapping;

namespace Edge.Data.Pipeline.Metrics.Services
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
			get { return base.ImportManager as AdMetricsImportManager; }
		}

		protected override MetricsDeliveryManager CreateImportManager(long serviceInstanceID, MetricsDeliveryManagerOptions options)
		{
			return new AdMetricsImportManager(serviceInstanceID, options);
		}

		protected override void LoadConfiguration()
		{
			if (!Mappings.Objects.TryGetValue(typeof(Ad), out _adMappings))
				throw new MappingConfigurationException("Missing mapping definition for Ad.", "Object");

			if (!Mappings.Objects.TryGetValue(typeof(AdMetricsUnit), out _metricsMappings))
				throw new MappingConfigurationException("Missing mapping definition for AdMetricsUnit.", "Object");

			if (!Mappings.Objects.TryGetValue(typeof(Signature), out _signatureMappings))
				throw new MappingConfigurationException("Missing mapping definition for Signature.", "Object");
		}

		protected override void OnRead()
		{
			var ad = new Ad();
			_adMappings.Apply(ad);
			ImportManager.ImportAd(ad);

			var metrics = new AdMetricsUnit {Ad = ad};
			_metricsMappings.Apply(metrics);

			var signature = new Signature();
			_signatureMappings.Apply(signature);

			//checking if signature is already exists in delivery outputs
			var outputs = from output in Delivery.Outputs
						  where output.Signature.Equals(signature.ToString())
						  select output;

			DeliveryOutput op = outputs.FirstOrDefault();
			if (op != null)
				//Attaching output to Metrics
				metrics.Output = op;
			else
			{
				var deliveryOutput = new DeliveryOutput
					{ 
					Signature = signature.Value, 
					TimePeriodStart =metrics.TimePeriodStart,
					TimePeriodEnd = metrics.TimePeriodEnd,
					Account = metrics.Ad.Account,
					Channel = metrics.Ad.Channel
				};
				Delivery.Outputs.Add(deliveryOutput);
				//Attaching output to Metrics
				metrics.Output = deliveryOutput;
			}

			ImportManager.ImportMetrics(metrics);
		}
	}
}
