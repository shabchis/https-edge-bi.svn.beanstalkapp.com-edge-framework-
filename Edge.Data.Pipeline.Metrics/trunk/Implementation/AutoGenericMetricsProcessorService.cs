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
	public abstract class GenericMetricsProcessorBase : AutoMetricsProcessorServiceBase
	{
		MappingContainer _metricsMappings;

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
		}

		protected override void OnRead()
		{
			var metrics = new GenericMetricsUnit();
			_metricsMappings.Apply(metrics);
			this.ImportManager.ImportMetrics(metrics);
		}
	}
}
