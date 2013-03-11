namespace Edge.Data.Pipeline.Metrics.Misc
{
	public static class Consts
	{
		public static class DeliveryHistoryParameters
		{
			public const string TablePerfix = "TablePerfix";

			public const string DeliveryMetricsTableName = "DeliveryMetricsTableName";
			public const string StagingMetricsTableName = "StagingMetricsTableName";
			public const string CommitMetricsTableName = "CommitMetricsTableName";

			public const string TransformTimestamp = "TransformTimestamp";
		}

		public static class AppSettings
		{
			public const string BufferSize = "BufferSize";
			public const string SqlTransformCommand = "Sql.TransformCommand";
			public const string SqlStageCommand = "Sql.StageCommand";
			public const string SqlCommitCommand = "Sql.CommitCommand";
			public const string SqlRollbackCommand = "Sql.RollbackCommand";
		}

		public static class ConnectionStrings
		{
			public const string Objects = "Edge.Objects";
			public const string Deliveries = "Edge.Deliveries";
			public const string Staging = "Edge.Staging";
			public const string DataWarehouse = "Edge.Dwh";
			public const string System = "Edge.System";
		}

		public static class ConfigurationOptions
		{
			public const string ImportManagerType = "ImportManagerType";
			public const string ReaderAdapterType = "ReaderAdapterType";
			public const string ChecksumTheshold = "ChecksumTheshold";
			public const string RollbackDeliveries = "RollbackDeliveries";
			public const string RollbackOutputs = "RollbackOutputs";
			public const string RollbackTableName = "RollbackTableName";
			public const string RollbackByDeliverisStoredProc = "RollbackByDeliverisStoredProc";

			public static string RollbackByOutputsStoredProc = "RollbackByOutputsStoredProc";
		}
	}
}
