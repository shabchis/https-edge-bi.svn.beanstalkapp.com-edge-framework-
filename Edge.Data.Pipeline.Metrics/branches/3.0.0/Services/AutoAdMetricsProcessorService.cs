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

		protected override MetricsUnit CreateEmptyMetricsUnit()
		{
			return new AdMetricsUnit();
		}
		#endregion
	}
}
