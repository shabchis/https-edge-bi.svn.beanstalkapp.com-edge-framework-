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
	public class AdMetricsProcessorService : MetricsProcessorServiceBase
	{
		MappingContainer _adMappings;
		MappingContainer _metricsMappings;

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
		}

		
		protected override void OnRead()
		{
			var ad = new Ad();
			_adMappings.Apply(ad);
			this.ImportManager.ImportAd(ad);

			var metrics = new AdMetricsUnit();
			_metricsMappings.Apply(metrics);
			this.ImportManager.ImportMetrics(metrics);
		}
	}
}
