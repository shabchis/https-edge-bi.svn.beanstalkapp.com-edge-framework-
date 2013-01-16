using System;
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

		protected override MetricsUnit CreateEmptyMetricsUnit()
		{
			return new GenericMetricsUnit();
		}
		#endregion
	}
}
