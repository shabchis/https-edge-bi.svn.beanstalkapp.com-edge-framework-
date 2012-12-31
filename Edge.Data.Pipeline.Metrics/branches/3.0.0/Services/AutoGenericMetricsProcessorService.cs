using System.Linq;
using Edge.Data.Objects;
using Edge.Data.Pipeline.Metrics.Base;
using Edge.Data.Pipeline.Mapping;
using Edge.Data.Pipeline.Metrics.GenericMetrics;

namespace Edge.Data.Pipeline.Metrics.Services
{
	/// <summary>
	/// Base class for ad metrics processors.
	/// </summary>
	public class AutoGenericMetricsProcessorService : AutoMetricsProcessorServiceBase
	{
		MappingContainer _metricsMappings;
		MappingContainer _signatureMappings;

		public new GenericMetricsImportManager ImportManager
		{
			get { return (GenericMetricsImportManager)base.ImportManager; }
		}

		protected override MetricsDeliveryManager CreateImportManager(long serviceInstanceID, MetricsDeliveryManagerOptions options)
		{
			return new GenericMetricsImportManager(serviceInstanceID, options);
		}

		protected override void LoadConfiguration()
		{
			if (!Mappings.Objects.TryGetValue(typeof(GenericMetricsUnit), out _metricsMappings))
				throw new MappingConfigurationException("Missing mapping definition for GenericMetricsUnit.", "Object");
			
			if (!Mappings.Objects.TryGetValue(typeof(Signature), out _signatureMappings))
				throw new MappingConfigurationException("Missing mapping definition for Signature.", "Object");
		}

		protected override void OnRead()
		{
			var metrics = new GenericMetricsUnit();
			_metricsMappings.Apply(metrics);

            //Writing to Log 
            //if (metrics.Output.Checksum.Count() == 0)
            //{
            //    Edge.Core.Utilities.Log("Output checksum is empty",Core.Utilities.LogMessageType.Information);
            //}

			var signature = new Signature();
			_signatureMappings.Apply(signature);

			//checking if signature is already exists in delivery outputs
			var outputs = from output in Delivery.Outputs
						  where output.Signature.Equals(signature.ToString())
						  select output;

			var op = outputs.FirstOrDefault();
			if (op != null)
				//Attaching output to Metrics
				metrics.Output = op;
			else
			{
				var deliveryOutput = new DeliveryOutput
					{
					Signature = signature.Value,
					TimePeriodStart = metrics.TimePeriodStart,
					TimePeriodEnd = metrics.TimePeriodEnd,
					Account = metrics.Account,
					Channel = metrics.Channel
				};
				Delivery.Outputs.Add(deliveryOutput);
				//Attaching output to Metrics
				metrics.Output = deliveryOutput;
			}

			ImportManager.ImportMetrics(metrics);
		}
	}
}
