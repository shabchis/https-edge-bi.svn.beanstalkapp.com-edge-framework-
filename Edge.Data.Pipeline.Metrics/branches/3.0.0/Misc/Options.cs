using Edge.Data.Objects;

namespace Edge.Data.Pipeline.Metrics.Misc
{
	public enum OptionsMatching
	{
		Any = 0,
		All = 1,
		Without = -1
	}

	public class MetricsDeliveryManagerOptions
	{
		public string StagingConnectionString { get; set; }
		public string CommitConnectionString { get; set; }
		public string ObjectsConnectionString { get; set; }

		public string SqlTransformCommand { get; set; }
		public string SqlStageCommand { get; set; }
		public string SqlCommitCommand { get; set; }
		public string SqlRollbackCommand { get; set; }
		public double ChecksumThreshold { get; set; }
		public MeasureOptions MeasureOptions { get; set; }
		public OptionsMatching MeasureOptionsMatch { get; set; }
		//public MetaPropertyOptions MetaPropertyOptions { get; set; }
		public OptionsMatching MetaPropertyOptionsMatch { get; set; }
		public bool IdentityInDebug { get; set; } // indication if to run Identity Manager in .NET debug mode or using SQL CLR (default)
	}
}