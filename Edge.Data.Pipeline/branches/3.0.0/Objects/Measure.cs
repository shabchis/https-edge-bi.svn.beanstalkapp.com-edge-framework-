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
        public int BaseMeasureID;
		public Account Account;
		public string Name;
        public Channel Channel;
		public string OltpName;
		public string DisplayName;
		public string SourceName;
        public string StringFormat;
        public int? AcquisitionNum;
		public MeasureOptions Options;
		public bool USDRequired;

		public static Dictionary<string, Measure> GetMeasures(Account account, Channel channel, SqlConnection connection, MeasureOptions? options = null,OptionsOperator @operator = OptionsOperator.Or, bool includeBase = false)
		{
			SqlCommand cmd = DataManager.CreateCommand(AppSettings.Get(typeof(Measure),"GetMeasures.SP"),
				System.Data.CommandType.StoredProcedure);
			cmd.Connection = connection;

			cmd.Parameters["@accountID"].Value = account == null ? DBNull.Value : (object)account.ID;
			cmd.Parameters["@channelID"].Value = channel == null ? DBNull.Value : (object)channel.ID;
			cmd.Parameters["@flags"].Value = options;
			cmd.Parameters["@operator"].Value = @operator;
            cmd.Parameters["@includeBase"].Value = includeBase;

			List<Measure> measures = new List<Measure>();
			using (SqlDataReader reader = cmd.ExecuteReader())
			{
				while (reader.Read())
				{
					Measure m = new Measure()
					{
                        ID = (int)reader["MeasureID"],
                        BaseMeasureID = (int)reader["BaseMeasureID"],
						Account = reader.Get<int>("AccountID") == -1 ? null : account,
                        Channel = reader.Get<int>("ChannelID") == -1 ? null : (channel ?? new Channel() { ID = reader.Get<int>("ChannelID") }),
                        Name = reader["Name"] is DBNull ? string.Empty : (string)reader["Name"],
                        DisplayName = reader["DisplayName"] is DBNull ? string.Empty : (string)reader["DisplayName"],
						SourceName = reader["SourceName"] is DBNull ? string.Empty : (string)reader["SourceName"],
						OltpName = reader["FieldName"] is DBNull ? string.Empty : (string)reader["FieldName"],
                        StringFormat = reader["StringFormat"] is DBNull ? string.Empty : (string)reader["StringFormat"],
                        AcquisitionNum = reader["AcquisitionNum"] is DBNull ? null : (int?)reader["AcquisitionNum"],
						Options = (MeasureOptions)reader["Flags"],
						USDRequired = reader.Get<bool>("Required_USD_Convert"),
					};

					measures.Add(m);
				}
			}

			return measures.ToDictionary(m => m.Name);
		}


		public double GetValueInUSD(SqlConnection connection,object valueToConvert)
		{
			SqlCommand cmd = DataManager.CreateCommand(AppSettings.Get(typeof(Measure), "GetMeasuresValueUSD.SP"),
				System.Data.CommandType.StoredProcedure);
			cmd.Connection = connection;
			cmd.Parameters["@Currency"].Value = valueToConvert;

			using (SqlDataReader reader = cmd.ExecuteReader())
			{
				if (reader.Read())
					return Convert.ToDouble(reader[0]);
				else
					throw new Exception("Could not convert value to USD");
			}
		}
	}

	[Flags]
	public enum MeasureOptions
	{
		None = 0x0,
		//IsDefault = 0x40,
		IsBackOffice = 0x04,
		IsTarget = 0x02,
		IsCalculated = 0x10,
		ValidationRequired = 0x80,
		All = 0xff
	}

	public enum OptionsOperator
	{
		Or = 0,
		And = 1,
		Not = -1
	}

}
