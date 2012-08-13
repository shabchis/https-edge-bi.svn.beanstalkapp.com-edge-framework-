using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Pipeline.Metrics
{
	public static class Consts
	{
		public static class DeliveryHistoryParameters
		{
			public const string TablePerfix = "TablePerfix";
			public const string MeasureNamesSql = "MeasureNamesSql";
			public const string MeasureFieldsSql = "MeasureFieldsSql";
			public const string MeasureValidateSql = "MeasureValidateSql";
			public const string CommitTableName = "CommitTableName";
		}

		public static class AppSettings
		{
			public const string BufferSize = "BufferSize";
			public const string SqlTransformCommand = "Sql.TransformCommand";
			public const string SqlStageCommand = "Sql.StageCommand";
			public const string SqlRollbackCommand = "Sql.RollbackCommand";
			
		}

		public static class ConnectionStrings
		{
			public const string StagingDatabase = "StagingDatabase";
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
