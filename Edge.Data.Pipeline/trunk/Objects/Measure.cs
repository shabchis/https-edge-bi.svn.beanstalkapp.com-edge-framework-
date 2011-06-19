using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SqlClient;
using Edge.Core.Data;

namespace Edge.Data.Objects
{
	public class Measure
	{
		public int ID;
		public Account Account;
		public string Name;
		public MeasureType MeasureType;
		public string OltpName;
		public string DisplayName;

		public static readonly Measure Cost					= new Measure() { ID = -601};
		public static readonly Measure Impressions			= new Measure() { ID = -602};
		public static readonly Measure UniqueImpressions	= new Measure() { ID = -603};
		public static readonly Measure Clicks				= new Measure() { ID = -604};
		public static readonly Measure UniqueClicks			= new Measure() { ID = -605};
		public static readonly Measure AveragePosition		= new Measure() { ID = -606};

		public static Measure[] GetMeasuresForAccount(Account account, SqlConnection connection)
		{
			SqlCommand cmd = DataManager.CreateCommand(@"Measure_GetMeasure(
				@accountID:Int,
				@measureID:Int,
				@includeBase:bit,
				@flags:int",
			System.Data.CommandType.StoredProcedure);
			cmd.Connection = connection;

			cmd.Parameters["@accountID"].Value = account.ID;
			cmd.Parameters["@flags"].Value = (int)MeasureOption.All - MeasureOption.IsTarget;

			List<Measure> measures = new List<Measure>();
			using (SqlDataReader reader = cmd.ExecuteReader())
			{
				while (reader.Read())
				{
					Measure m = new Measure()
					{
						ID = (int) reader["MeasureID"],
						Account = account,
						Name = (string) reader["MeasureName"],
						OltpName = (string) reader["FieldName"],
						MeasureType = (MeasureType) reader["MeasureType"]
					};

					measures.Add(m);
				}
			}

			return measures.ToArray();
		}

		[Flags]
		enum MeasureOption
		{
			IsTarget = 0x2,
			All = 0xff
		}
	}

	public enum MeasureType
	{
		Cost,	
		Impressions,
		UniqueImpressions,
		Clicks,	
		UniqueClicks,
		AveragePosition,
		Other
	}

}
