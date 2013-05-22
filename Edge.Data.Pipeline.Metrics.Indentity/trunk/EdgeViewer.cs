using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using Edge.Data.Objects;
using Microsoft.SqlServer.Server;

namespace Edge.Data.Pipeline.Metrics.Indentity
{
	/// <summary>
	/// Supply SELECt clauses for EdgeObjects and Metrics
	/// </summary>
	public static class EdgeViewer
	{
		#region Public Methods
		/// <summary>
		/// Per each type combine flat SELECT fields by real names in Metrics 
		/// </summary>
		/// <param name="accountId"></param>
		/// <param name="connection"></param>
		/// <param name="pipe">Pipe to send SQL rows reply</param>
		public static void GetObjectsView(int accountId, SqlConnection connection, SqlPipe pipe)
		{
			// load configuration
			var edgeTypes = EdgeObjectConfigLoader.LoadEdgeTypes(accountId, connection);
			var edgeFields = EdgeObjectConfigLoader.LoadEdgeFields(accountId, edgeTypes, connection);
			EdgeObjectConfigLoader.SetEdgeTypeEdgeFieldRelation(accountId, edgeTypes, edgeFields, connection);

			// prepare result record
			var record = new SqlDataRecord(new[] 
			{	
				new SqlMetaData("TypeID", SqlDbType.Int), 
				new SqlMetaData("Name", SqlDbType.NVarChar, 50),
				new SqlMetaData("Select", SqlDbType.NVarChar, 1000)
			});
			pipe.SendResultsStart(record);

			foreach (var type in edgeTypes.Values.Where(x => x.IsAbstract == false))
			{
				// prepare type fields SELECT
				var fieldsStr = String.Empty;
				foreach (var field in type.Fields)
				{
					fieldsStr = String.Format("{0}{1} AS {2}, ", fieldsStr, field.ColumnNameGK, field.FieldNameGK);
					if (field.Field.FieldEdgeType == null) continue;

					// add to select all options of child edge types
					foreach (var childType in EdgeObjectConfigLoader.FindEdgeTypeInheritors(field.Field.FieldEdgeType, edgeTypes))
					{
						if (childType == field.Field.FieldEdgeType) continue;
						fieldsStr = String.Format("{0}{1} AS {2}_{3}_gk, ", fieldsStr, field.ColumnNameGK, field.Field.Name, childType.Name);
					}
				}
				if (fieldsStr.Length <= 0) continue;

				fieldsStr = fieldsStr.Remove(fieldsStr.Length - 2, 2);
				var select = String.Format("SELECT {0} FROM {1} WHERE TYPEID={2}", fieldsStr, type.TableName, type.TypeID);

				// set report and and it
				record.SetInt32(0, type.TypeID);
				record.SetString(1, type.Name);
				record.SetString(2, select);

				pipe.SendResultsRow(record);
			}
			pipe.SendResultsEnd();
		}

		/// <summary>
		/// Build Metics SELECT from Delivery Metrics table with JOINs all delivery objects by TKs 
		/// in order to fill Staging Metrics table found by Best Match from Delivery Metrics table
		/// </summary>
		public static string GetMetricsView(int accountId, string tableName, SqlConnection connection)
		{
			var selectStr = String.Empty;
			var fromStr = String.Format("\t[EdgeDeliveries].{0} AS Metrics\n", tableName);
			var tablePrefix = tableName.ToLower().Replace("_metrics]", "").Replace("[dbo].[", "");

			var edgeTypes = EdgeObjectConfigLoader.LoadEdgeTypes(accountId, connection);
			var sql = String.Format("SELECT EdgeFieldName, ParentFieldName, EdgeTypeID, MeasureName FROM [EdgeDeliveries].[dbo].[MD_MetricsMetadata] WHERE TABLENAME='{0}'", tableName);

			using (var cmd = new SqlCommand(sql, connection))
			{
				using (var reader = cmd.ExecuteReader())
				{
					while (reader.Read())
					{
						// add dimension field (JOIN according TK to find GK and WHERE by type ID)
						if (!String.IsNullOrEmpty(reader["EdgeFieldName"].ToString()))
						{
							var edgeType = edgeTypes.Values.FirstOrDefault(x => x.TypeID == int.Parse(reader["EdgeTypeID"].ToString()));
							if (edgeType == null) continue;

							var fieldName = reader["EdgeFieldName"].ToString().Replace("_gk", "");
							var parentFieldName = reader["ParentFieldName"].ToString().Replace("_gk", "");

							selectStr = String.Format("{0}\t{1}.GK AS {1}_gk,\n", selectStr, fieldName);
							fromStr = String.Format("{0}\tINNER JOIN {1} AS {3} ON Metrics.{2}_tk={3}.TK\n",
														fromStr, GetTableName(tablePrefix, edgeType.TableName), parentFieldName, fieldName);
						}
						// add measure fields
						else if (!String.IsNullOrEmpty(reader["MeasureName"].ToString()))
						{
							selectStr = String.Format("{0}\tMetrics.{1},\n", selectStr, reader["MeasureName"]);
						}
					}
				}
			}
			// combine select
			return selectStr.Length == 0 ?
					"No data relevant data in MD_MetricsMetadata" :
					String.Format("SELECT\n{0}\nFROM\n{1}", selectStr.Remove(selectStr.Length - 2, 2), fromStr);
		}

		/// <summary>
		/// Perfrom metrics staging: insert all metrics table data into staging table
		/// </summary>
		/// <param name="accountId"></param>
		/// <param name="deliveryTableName"></param>
		/// <param name="stagingTableName"></param>
		/// <param name="connection"></param>
		public static string StageMetrics(int accountId, string deliveryTableName, string stagingTableName, SqlConnection connection)
		{
			var selectStr = String.Empty;
			var insertStr = String.Empty;
			var fromStr = String.Format("\t[EdgeDeliveries].{0} AS Metrics\n", deliveryTableName);
			var tablePrefix = deliveryTableName.ToLower().Replace("_metrics]", "").Replace("[dbo].[", "");

			if (!stagingTableName.ToLower().Contains("edgeetaging"))
				stagingTableName = String.Format("[EdgeStaging].{0}", stagingTableName);

			var edgeTypes = EdgeObjectConfigLoader.LoadEdgeTypes(accountId, connection);

			// System Fields (e.g. Account, Channel, time, etc.)
			using (var cmd = new SqlCommand("SELECT SystemField FROM [EdgeStaging].[dbo].[SystemFields]", connection))
			{
				using (var reader = cmd.ExecuteReader())
				{
					while (reader.Read())
					{
						selectStr = String.Format("{0}\tMetrics.{1},\n", selectStr, reader["SystemField"]);
						insertStr = String.Format("{0}\t{1},\n", insertStr, reader["SystemField"]);
					}
				}
			}

			// metrics fields (according to metrics metadata table) 
			var sql = String.Format("SELECT EdgeFieldName, ParentFieldName, EdgeTypeID, MeasureName FROM [EdgeDeliveries].[dbo].[MD_MetricsMetadata] WHERE TABLENAME='{0}'", deliveryTableName);
			using (var cmd = new SqlCommand(sql, connection))
			{
				using (var reader = cmd.ExecuteReader())
				{
					while (reader.Read())
					{
						// add dimension field (JOIN according TK to find GK and WHERE by type ID)
						if (!String.IsNullOrEmpty(reader["EdgeFieldName"].ToString()))
						{
							var edgeType = edgeTypes.Values.FirstOrDefault(x => x.TypeID == int.Parse(reader["EdgeTypeID"].ToString()));
							if (edgeType == null) continue;

							var fieldName = reader["EdgeFieldName"].ToString().Replace("_gk", "");
							var parentFieldName = reader["ParentFieldName"].ToString().Replace("_gk", "");

							selectStr = String.Format("{0}\t{1}.GK AS {1}_gk,\n", selectStr, fieldName);
							insertStr = String.Format("{0}\t{1}_gk,\n", insertStr, fieldName);
							fromStr = String.Format("{0}\tINNER JOIN {1} AS {3} ON Metrics.{2}_tk={3}.TK\n",
														fromStr, GetTableName(tablePrefix, edgeType.TableName), parentFieldName, fieldName);
						}
						// add measure fields
						else if (!String.IsNullOrEmpty(reader["MeasureName"].ToString()))
						{
							selectStr = String.Format("{0}\tMetrics.{1},\n", selectStr, reader["MeasureName"]);
							insertStr = String.Format("{0}\t{1},\n", insertStr, reader["MeasureName"]);
						}
					}
				}
			}

			// perform staging (insert metrics table data into staging table)
			sql = String.Format("INSERT INTO {0} ({1})\nSELECT {2}\nFROM {3}",
								stagingTableName,
								insertStr.TrimEnd(new[] { ',', '\n' }),
								selectStr.TrimEnd(new[] { ',', '\n' }),
								fromStr);

			return sql;
			//using (var cmd = new SqlCommand(sql, connection))
			//{
			//	cmd.ExecuteNonQuery();
			//}
		} 
		#endregion

		#region Private Methods
		private static string GetTableName(string tablePrefix, string tableName)
		{
			return String.Format("[EdgeDeliveries].[dbo].[{0}_{1}]", tablePrefix, tableName);
		} 
		#endregion
	}
}
