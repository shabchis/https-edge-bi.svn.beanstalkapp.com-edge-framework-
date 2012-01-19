using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SqlClient;
using Edge.Core.Data;
using Edge.Core.Configuration;

namespace Edge.Data.Objects
{
	public class Measure
	{
		#region Const
		public static class Common
		{
			public const string Cost = "Cost";
			public const string Impressions = "Impressions";
			public const string UniqueImpressions = "UniqueImpressions";
			public const string Clicks = "Clicks";
			public const string UniqueClicks = "UniqueClicks";
			public const string AveragePosition = "AveragePosition";
			public const string Other = "Other";
		}
		#endregion

		public int ID;
		public Account Account;
		public string Name;
		public string OltpName;
		public string DisplayName;
		public string SourceName;
		public MeasureOptions Options;

		public static Dictionary<string, Measure> GetMeasures(Account account, Channel channel, SqlConnection connection, MeasureOptions options, MeasureOptionsOperator @operator = MeasureOptionsOperator.And)
		{
			SqlCommand cmd = DataManager.CreateCommand(AppSettings.Get(typeof(Measure),"GetMeasures.SP"),
				System.Data.CommandType.StoredProcedure);
			cmd.Connection = connection;

			cmd.Parameters["@accountID"].Value = account == null ? DBNull.Value : (object)account.ID;
			cmd.Parameters["@channelID"].Value = channel == null ? DBNull.Value : (object)channel.ID;
			cmd.Parameters["@flags"].Value = options;
			cmd.Parameters["@operator"].Value = @operator;

			List<Measure> measures = new List<Measure>();
			using (SqlDataReader reader = cmd.ExecuteReader())
			{
				while (reader.Read())
				{
					Measure m = new Measure()
					{
						ID = (int) reader["MeasureID"],
						Account = reader.Get<int>("AccountID") == -1 ? null : account,
						Name =(string)reader["Name"],
						DisplayName = (string)reader["DisplayName"],
						SourceName = reader["SourceName"] is DBNull ? string.Empty : (string)reader["SourceName"],
						OltpName = (string) reader["FieldName"],
						Options = (MeasureOptions)reader["Flags"]
					};

					measures.Add(m);
				}
			}

			return measures.ToDictionary(m => m.Name);
		}

		
	}

	[Flags]
	public enum MeasureOptions
	{
		//IsDefault = 0x40,
		IsBackOffice = 0x04,
		IsTarget = 0x02,
		IsCalculated = 0x10,
		ValidationRequired = 0x80,
		All = 0xff
	}

	public enum MeasureOptionsOperator
	{
		Or = 0,
		And = 1,
		Not = -1
	}

}
