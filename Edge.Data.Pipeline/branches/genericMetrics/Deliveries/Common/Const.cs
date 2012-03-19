using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Pipeline.Common.Importing
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
		}

		public static class AppSettings
		{
			public const string BufferSize = "BufferSize";
			public const string SqlPrepareCommand = "SQL.PrepareCommand";
			public const string SqlCommitCommand = "SQL.CommitCommand";
			public const string SqlRollbackCommand = "SQL.RollbackCommand";
			public const string CommitValidationTheshold = "CommitValidationTheshold";
		}

		public static class ConnectionStrings
		{
			public const string Oltp = "OLTP";
		}

		public static class ConfigurationOptions
		{
			public const string FileFormat = "FileFormat";
			public const string Compression = "Compression";
			public const string DeliveryFileName = "DeliveryFileName";
		}

		public static class HistoryParameters
		{
			public const string ValidationInputs = "ValidationInputs";
		}
	}
}
