﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Data;
using System.Data.SqlClient;
using Edge.Core.Configuration;
using Edge.Core.Utilities;
using Edge.Data.Objects;
using Edge.Data.Pipeline.Objects;

namespace Edge.Data.Pipeline.Metrics.Managers
{
	/// <summary>
	/// Table manager class is used for:
	/// * Delivery: create metrics table and import data into it
	/// * Staging: find matching table for Staging
	/// </summary>
	internal class MetricsTableManager
	{
		#region Column class
		public class Column
		{
			public string Name { get; set; }
			public SqlDbType DbType { get; set; }
			public int Size { get; set; }
			public object Value { get; set; }
			public bool Nullable { get; set; }
			public string DefaultValue { get; set; }
		} 
		#endregion

		#region Data Members
		private const string EGDE_OBJECTS_SUFFIX = "Usid";
		string _tablePrefix;

		readonly SqlConnection _sqlConnection;
		readonly EdgeObjectsManager _edgeObjectsManger;

		readonly Dictionary<string, Column> _columns = new Dictionary<string, Column>();
		#endregion

		#region Ctor
		public MetricsTableManager(SqlConnection connection, EdgeObjectsManager edgeObjectsManager)
		{
			_sqlConnection = connection;
			_edgeObjectsManger = edgeObjectsManager;
		} 
		#endregion

		#region Delivery Metrics

		#region Create delivery metrics table
		/// <summary>
		/// Create delivery metric table named by table prefix using sample metric unit structure
		/// </summary>
		/// <param name="tablePerifx">delivery metric table prefix</param>
		/// <param name="metricsUnit">sample metric unit for table structure</param>
		/// <returns></returns>
		public string CreateDeliveryMetricsTable(string tablePerifx, MetricsUnit metricsUnit)
		{
			_tablePrefix = tablePerifx;

			var flatObjectList = _edgeObjectsManger.GetFlatObjectList(metricsUnit);

			var columnList = GetColumnList(flatObjectList, false); // sampe metrics objects are not added to object cache

			return CreateTable(columnList);
		}

		/// <summary>
		/// Build DML statement to create delivery table according to the column list
		/// Run DML statement
		/// </summary>
		/// <param name="columnList"></param>
		/// <returns></returns>
		private string CreateTable(IEnumerable<Column> columnList)
		{
			var builder = new StringBuilder();
			var tableName = string.Format("{0}_Metrics", _tablePrefix);
			builder.AppendFormat("CREATE TABLE  [dbo].[{0}](\n", tableName);

			foreach (var col in columnList)
			{
				builder.AppendFormat("\t[{0}] [{1}] {2} {3} {4}, \n",
					col.Name,
					col.DbType,
					col.Size != 0 ? string.Format("({0})", col.Size) : null,
					col.Nullable ? "null" : "not null",
					!string.IsNullOrWhiteSpace(col.DefaultValue) ? string.Format("Default {0}", col.DefaultValue) : string.Empty
				);
			}
			builder.Remove(builder.Length - 3, 3);
			builder.Append(");");
			using (var command = new SqlCommand(builder.ToString(), _sqlConnection))
			{
				command.CommandTimeout = 80; //DEFAULT IS 30 AND SOMTIME NOT ENOUGH WHEN RUNING CUBE
				command.ExecuteNonQuery();
			}

			return tableName;
		} 
		#endregion

		#region Import metrics
		/// <summary>
		/// Save metrics (sigle row) into metrics table
		/// </summary>
		/// <param name="metricsUnit"></param>
		public void ImportMetrics(MetricsUnit metricsUnit)
		{
			var flatObjectList = _edgeObjectsManger.GetFlatObjectList(metricsUnit);

			var columnList = GetColumnList(flatObjectList);

			ImportMetricsData(columnList);
		}

		private void ImportMetricsData(IEnumerable<Column> columnList)
		{
			var columnsStr = String.Empty;
			var valuesStr = String.Empty;

			foreach (var column in columnList)
			{
				columnsStr = String.Format("{0}\n{1},", columnsStr, column.Name);
				valuesStr = String.Format("{0}\n{1},", valuesStr, column.DbType == SqlDbType.NVarChar ? String.Format("'{0}'", column.Value) :
																	column.DbType == SqlDbType.DateTime ? String.Format("'{0}'", ((DateTime)column.Value).ToString("yyyy-MM-dd HH:mm:ss")) :
																	column.Value);
			}

			// remove last commas
			if (columnsStr.Length > 1) columnsStr = columnsStr.Remove(columnsStr.Length - 1, 1);
			if (valuesStr.Length > 1) valuesStr = valuesStr.Remove(valuesStr.Length - 1, 1);

			// prepare and execute INSERT
			var insertSql = String.Format("INSERT INTO [DBO].[{0}_Metrics] ({1}) VALUES ({2});", _tablePrefix, columnsStr, valuesStr);
			using (var command = new SqlCommand(insertSql, _sqlConnection))
			{
				command.ExecuteNonQuery();
			}
		} 
		#endregion

		/// <summary>
		/// get column list from falt object list
		/// </summary>
		/// <param name="objectList"></param>
		/// <param name="addToCache">indication if to add to edge object to cache for later instert into EdgeObject tables (on ImportEnd)
		/// TRUE : default, when import metrics
		/// FALSE: when creating delivery table by metrics sample</param>
		/// <returns></returns>
		private IEnumerable<Column> GetColumnList(IEnumerable<object> objectList, bool addToCache = true)
		{
			var columnDict = new Dictionary<string, Column>();
			foreach (var obj in objectList)
			{
				if (obj is EdgeObject)
				{
					foreach (var column in CreateEdgeObjColumns(obj as EdgeObject).Where(column => !columnDict.ContainsKey(column.Name)))
					{
						columnDict.Add(column.Name, column);
					}
					// EdgeObject are added to cache in order to insert them later into object tables
					if (addToCache)
						_edgeObjectsManger.AddToCache(obj as EdgeObject);
				}
				else
				{
					var column = CreateColumn(obj);
					if (!columnDict.ContainsKey(column.Name))
						columnDict.Add(column.Name, column);
				}
			}
			return columnDict.Values.ToList();
		}

		/// <summary>
		/// Create columns for edge oject (GK and TK)
		/// </summary>
		/// <param name="obj"></param>
		/// <returns></returns>
		private IEnumerable<Column> CreateEdgeObjColumns(EdgeObject obj)
		{
			var columnList = new List<Column>();

			if (obj.EdgeType == null)
				throw new ConfigurationErrorsException(String.Format("EdgeType is not set for object {0}", obj.GetType()));

			// add 2 columns: GK and TK (temp key)
			columnList.Add(new Column { Name = String.Format("{0}_GK", obj.EdgeType.Name), Value = obj.GK });
			columnList.Add(new Column { Name = String.Format("{0}_TK", obj.EdgeType.Name), Value = obj.TK, DbType = SqlDbType.NVarChar, Size = 500 });

			// add columns for all childs
			if (obj.HasChildsObjects)
			{
				foreach (var child in obj.GetChildObjects())
				{
					columnList.AddRange(CreateEdgeObjColumns(child));
				}
			}
			return columnList;
		}

		/// <summary>
		/// Create column from object accoding to the object type
		/// </summary>
		/// <param name="obj"></param>
		/// <returns></returns>
		private Column CreateColumn(object obj)
		{
			string clmName;
			object clmValue;
			var isNullable = false;
			var clmType = SqlDbType.BigInt;

			if (obj is KeyValuePair<Measure, double>)
			{
				var measure = (KeyValuePair<Measure, double>)obj;
				clmName = measure.Key.DataType == MeasureDataType.Currency ? string.Format("{0}_Converted", measure.Key.Name) : measure.Key.Name;
				clmValue = measure.Value;
			}
			else if (obj is ConstEdgeField)
			{
				var field = obj as ConstEdgeField;
				clmName = field.Name;
				clmValue = field.Value;
				clmType = Convert2DbType(field.Type);
				isNullable = true;
			}
			else
				throw new ArgumentException(String.Format("Unknown object type '{0}' for creating metrics columns", obj.GetType()));

			return new Column { Name = clmName, Value = clmValue, DbType = clmType, Nullable = isNullable };
		}

		private static SqlDbType Convert2DbType(Type type)
		{
			return type == typeof(int) ? SqlDbType.Int :
					type == typeof(Guid) ? SqlDbType.VarChar :
					type == typeof(DateTime) ? SqlDbType.DateTime :
					type == typeof(double) ? SqlDbType.Float :
					SqlDbType.NVarChar;
		}

		#endregion

		#region Staging
		public string FindStagingTable(string metricsTableName)
		{
			string stagingTableName;
			using (SqlCommand command = SqlUtility.CreateCommand(AppSettings.Get(this, "SP_FindStagingTable"), CommandType.StoredProcedure))
			{
				command.Parameters["@templateTable"].Value = metricsTableName;
				command.Parameters["@templateDB"].Value = ""; //TODO: FROM WHERE DO i TAKE THIS TABLE?
				command.Parameters["@searchDB"].Value = ""; //TODO: FROM WHERE DO i TAKE THIS TABLE?
				using (SqlDataReader reader = command.ExecuteReader())
				{
					if (!reader.Read())
						throw new Exception("No staging table   Found");
					
					stagingTableName = reader["TABLE_NAME"].ToString();
				}
			}
			return stagingTableName;
		}

		internal void Staging(string deliveryTable, string stagingTable)
		{
			List<Column> cols = _columns.Values.ToList();
			var builder = new StringBuilder();
			builder.AppendFormat("INSERT INTO {0}\n(", stagingTable);
			for (int i = 0; i < cols.Count; i++)
			{
				builder.AppendFormat(i != cols.Count - 1 ? "\t{0},\n" : "\t{0})\n", cols[i].Name);
			}
			builder.Append("\tVALUES (SELECT\n");
			for (int i = 0; i < cols.Count; i++)
			{
				string colName = cols[i].Name.Contains(EGDE_OBJECTS_SUFFIX) ? "GKS.GK" : string.Format("{0}.{1}", "Metrics", cols[i].Name);

				builder.AppendFormat(i != cols.Count - 1 ? "\t{0},\n" : "\t{0})\n", colName);
			}

			builder.Append("\tWHERE (\n");
			bool firstFilter = true;
			for (int i = 0; i < cols.Count; i++)
			{
				string filter = string.Empty;
				if (cols[i].Name.Contains(EGDE_OBJECTS_SUFFIX))
				{
					if (firstFilter)
					{
						filter = string.Format("Metrics.{0}=GKS.Usid\n", cols[i].Name);
						firstFilter = false;
					}
					else
						filter = string.Format("AND Metrics.{0}=GKS.Usid\n", cols[i].Name);
				}

				if (i != cols.Count - 1)
				{
					filter = string.IsNullOrEmpty(filter) ? "\t)" : string.Format("\t{0})\n", filter);
				}

				if (!string.IsNullOrEmpty(filter))
					builder.Append(filter);

			}
			using (var command = new SqlCommand(builder.ToString(), _sqlConnection))
			{
				command.ExecuteNonQuery();
			}
		} 
		#endregion
	}
}