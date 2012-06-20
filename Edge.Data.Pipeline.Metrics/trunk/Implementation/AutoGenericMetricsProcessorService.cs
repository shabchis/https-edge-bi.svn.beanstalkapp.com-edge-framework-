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

namespace Edge.Data.Pipeline.Metrics.GenericMetrics
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

		protected override MetricsImportManager CreateImportManager(long serviceInstanceID, MetricsImportManagerOptions options)
		{
			return new GenericMetricsImportManager(serviceInstanceID, options);
		}

		protected override void LoadConfiguration()
		{
			if (!this.Mappings.Objects.TryGetValue(typeof(AdMetricsUnit), out _metricsMappings))
				throw new MappingConfigurationException("Missing mapping definition for AdMetricsUnit.", "Object");
			
			if (!this.Mappings.Objects.TryGetValue(typeof(Signature), out _signatureMappings))
				throw new MappingConfigurationException("Missing mapping definition for Signature.", "Object");
		}

		protected override void OnRead()
		{
			var metrics = new GenericMetricsUnit();
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
				(metrics as GenericMetricsUnit).Output = op;
			else
			{
				DeliveryOutput deliveryOutput = new DeliveryOutput()
				{
					Signature = signature.Value,
					TimePeriodStart = metrics.TimePeriodStart,
					TimePeriodEnd = metrics.TimePeriodEnd,
					Account = metrics.Account,
					Channel = metrics.Channel
				};
				this.Delivery.Outputs.Add(deliveryOutput);
				//Attaching output to Metrics
				(metrics as GenericMetricsUnit).Output = deliveryOutput;
			}

			this.ImportManager.ImportMetrics(metrics);
		}
	}
}
