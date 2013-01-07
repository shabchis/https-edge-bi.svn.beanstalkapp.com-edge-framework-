using System;
using System.Linq;
using Edge.Data.Objects;
using Edge.Data.Pipeline.Metrics.Base;
using Edge.Data.Pipeline.Metrics.Implementation;
using Edge.Data.Pipeline.Objects;

namespace Edge.Data.Pipeline.Metrics.Services
{
	/// <summary>
	/// Automatic generic data processing service
	/// </summary>
	public class AutoGenericMetricsProcessorService : AutoMetricsProcessorServiceBase
	{
		#region Properties
		public new GenericMetricsImportManager ImportManager
		{
			get { return (GenericMetricsImportManager)base.ImportManager; }
		} 
		#endregion

		#region Override Methods
		protected override MetricsDeliveryManager CreateImportManager(Guid serviceInstanceID, MetricsDeliveryManagerOptions options)
		{
			return new GenericMetricsImportManager(serviceInstanceID, options);
		}

		protected override void OnRead()
		{
			var metrics = new GenericMetricsUnit();
			MetricsMappings.Apply(metrics);

			//Writing to Log 
			//if (metrics.Output.Checksum.Count() == 0)
			//{
			//    Edge.Core.Utilities.Log("Output checksum is empty",Core.Utilities.LogMessageType.Information);
			//}

			var signature = new Signature();
			SignatureMappings.Apply(signature);

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

		#endregion
	}
}
