﻿using System;
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
			//var whereStr = String.Empty;
			var tablePrefix = tableName.ToLower().Replace("_metrics]", "").Replace("[dbo].[", "");

			var edgeTypes = EdgeObjectConfigLoader.LoadEdgeTypes(accountId, connection);
			var sql = String.Format("SELECT EdgeFieldName, EdgeTypeID, MeasureName FROM [EdgeDeliveries].[dbo].[MD_MetricsMetadata] WHERE TABLENAME='{0}' AND IsChildField = 0", tableName);
			
			using (var cmd = new SqlCommand(sql, connection))
			{
				using (var reader = cmd.ExecuteReader())
				{
					while (reader.Read())
					{
						// add dimension field (JOIN according TK to find GK and WHERE by type ID)
						if (!String.IsNullOrEmpty(reader["EdgeFieldName"].ToString()))
						{
							var fieldName = reader["EdgeFieldName"].ToString().Replace("_gk", "");
							var edgeType = edgeTypes.Values.FirstOrDefault(x => x.TypeID == int.Parse(reader["EdgeTypeID"].ToString()));
							if (edgeType == null) continue;

							foreach (var childType in EdgeObjectConfigLoader.FindEdgeTypeInheritors(edgeType, edgeTypes))
							{
								var childFieldName = childType == edgeType ? fieldName : String.Format("{0}_{1}", fieldName, childType.Name);
								
								selectStr = String.Format("{0}\t{1}.GK AS {2}_gk, {3} AS {2}_type,\n", selectStr, fieldName, childFieldName, childType.TypeID);
								if (childType == edgeType)
								{
									fromStr = String.Format("{0}\tINNER JOIN {1} AS {2} ON Metrics.{3}_tk={2}.TK\n",
														fromStr, GetTableName(tablePrefix, childType.TableName), childFieldName, fieldName);
									//whereStr = String.Format("{0}\t{1}.TYPEID={2} AND\n", whereStr, childFieldName, childType.TypeID);
								}
							}
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
			return selectStr.Length == 0  ?
					"No data relevant data in MD_MetricsMetadata" :
					String.Format("SELECT\n{0}\nFROM\n{1}", selectStr.Remove(selectStr.Length - 2, 2), fromStr);
					//String.Format("SELECT\n{0}\nFROM\n{1}WHERE\n{2}", selectStr.Remove(selectStr.Length - 2, 2), fromStr, whereStr.Remove(whereStr.Length - 5, 5));
		}
		
		private static string GetTableName(string tablePrefix, string tableName)
		{
			return String.Format("[EdgeDeliveries].[dbo].[{0}_{1}]", tablePrefix, tableName);
		}
	}
}
