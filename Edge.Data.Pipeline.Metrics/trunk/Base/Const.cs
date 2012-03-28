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
			public const string MeasureOltpFieldsSql = "MeasureOltpFieldsSql";
			public const string MeasureValidateSql = "MeasureValidateSql";
			public const string CommitTableName = "CommitTableName";
			public const string ChecksumTotals = "ChecksumTotals";
			public const string ChecksumInputs = "ChecksumInputs";
		}

		public static class AppSettings
		{
			public const string BufferSize = "BufferSize";
			public const string SqlPrepareCommand = "Sql.PrepareCommand";
			public const string SqlCommitCommand = "Sql.CommitCommand";
			public const string SqlRollbackCommand = "Sql.RollbackCommand";
			
		}

		public static class ConnectionStrings
		{
			public const string StagingDatabase = "StagingDatabase";
		}

		public static class ConfigurationOptions
		{
			public const string ImportManagerType = "ImportManagerType";
			public const string ChecksumTheshold = "ChecksumTheshold";
			public const string RollbackDeliveries = "RollbackDeliveries";
			public const string RollbackTableName = "RollbackTableName";
			public const string RollbackStoredProc = "RollbackStoredProc";
		}
	}
}
