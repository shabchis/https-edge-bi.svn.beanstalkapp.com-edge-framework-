using System;
using System.Linq;
using Edge.Data.Objects;
using Edge.Data.Pipeline.Metrics.Base;
using Edge.Data.Pipeline.Mapping;
using Edge.Data.Pipeline.Metrics.Implementation;
using Edge.Data.Pipeline.Objects;

namespace Edge.Data.Pipeline.Metrics.Services
{
	/// <summary>
	/// Automatic Ad data processing service
	/// </summary>
	public class AutoAdMetricsProcessorService : AutoMetricsProcessorServiceBase
	{
		#region Data Members
		private MappingContainer _adMappings;
		#endregion

		#region Properties
		public new AdMetricsImportManager ImportManager
		{
			get { return base.ImportManager as AdMetricsImportManager; }
		} 
		#endregion

		#region Override Methods
		protected override MetricsDeliveryManager CreateImportManager(Guid serviceInstanceID, MetricsDeliveryManagerOptions options)
		{
			return new AdMetricsImportManager(serviceInstanceID, options);
		}

		protected override void LoadConfiguration()
		{
			base.LoadConfiguration();

			if (!Mappings.Objects.TryGetValue(typeof(Ad), out _adMappings))
				throw new MappingConfigurationException("Missing mapping definition for Ad.", "Object");
		}

		protected override void OnRead()
		{
			var ad = new Ad();
			_adMappings.Apply(ad);
			ImportManager.ImportAd(ad);

			var metrics = new AdMetricsUnit { Ad = ad };
			MetricsMappings.Apply(metrics);

			var signature = new Signature();
			SignatureMappings.Apply(signature);

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
						TimePeriodStart = metrics.TimePeriodStart,
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

		protected override MetricsUnit CreateEmptyMetricsUnit()
		{
			return new AdMetricsUnit();
		}
		#endregion
	}
}
