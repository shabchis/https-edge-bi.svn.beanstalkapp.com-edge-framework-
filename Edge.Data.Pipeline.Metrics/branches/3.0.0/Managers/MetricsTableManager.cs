using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Data;
using System.Data.SqlClient;
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
		public string TableName { get; set; }

		private readonly SqlConnection _deliverySqlConnection;
		private readonly EdgeObjectsManager _edgeObjectsManger;

		private const string SP_FIND_BEST_MATCH_METRICS_TABLE = "EdgeStaging.dbo.sp_BestMatch";
		private const string SP_STAGE_DELIVERY_METRICS = "EdgeStaging.dbo.sp_MetrixStaging";
		#endregion

		#region Ctor
		public MetricsTableManager(SqlConnection connection, EdgeObjectsManager edgeObjectsManager)
		{
			_deliverySqlConnection = connection;
			_edgeObjectsManger = edgeObjectsManager;
		} 
		#endregion

		#region Delivery Metrics

		#region Create delivery metrics table

		/// <summary>
		/// Create delivery metric table named by table prefix using sample metric unit structure
		/// </summary>
		/// <param name="tablePrefix"></param>
		/// <param name="metricsUnit">sample metric unit for table structure</param>
		/// <returns></returns>
		public void CreateDeliveryMetricsTable(string tablePrefix, MetricsUnit metricsUnit)
		{
			TableName = string.Format("[DBO].[{0}_Metrics]", tablePrefix);

			var flatObjectList = _edgeObjectsManger.GetFlatObjectList(metricsUnit);

			SaveMetricsMetadata(flatObjectList);

			var columnList = GetColumnList(flatObjectList, false); // sampe metrics objects are not added to object cache

			CreateTable(columnList);
		}

		/// <summary>
		/// Build DML statement to create delivery table according to the column list
		/// Run DML statement
		/// </summary>
		/// <param name="columnList"></param>
		/// <returns></returns>
		private void CreateTable(IEnumerable<Column> columnList)
		{
			var builder = new StringBuilder();
			builder.AppendFormat("CREATE TABLE {0}(\n", TableName);

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
			using (var command = new SqlCommand(builder.ToString(), _deliverySqlConnection))
			{
				command.CommandTimeout = 80; //DEFAULT IS 30 AND SOMTIME NOT ENOUGH WHEN RUNING CUBE
				command.ExecuteNonQuery();
			}
		}

		/// <summary>
		/// Save current delivery metrics table structure to find best match Metrics table (Staging)
		/// </summary>
		/// <param name="flatObjectList"></param>
		private void SaveMetricsMetadata(IEnumerable<object> flatObjectList)
		{
			using (var cmd = new SqlCommand())
			{
				cmd.Connection = _deliverySqlConnection;
				cmd.CommandText = "INSERT INTO DBO.MD_MetricsMetadata ([TableName], [EdgeFieldID], [EdgeFieldName], [EdgeTypeID], [MeasureName]) " +
								  "VALUES (@TableName, @EdgeFieldID, @EdgeFieldName, @EdgeTypeID, @MeasureName)";

				cmd.Parameters.Add(new SqlParameter("@TableName", TableName));
				cmd.Parameters.Add(new SqlParameter("@EdgeFieldID", null));
				cmd.Parameters.Add(new SqlParameter("@EdgeFieldName", null));
				cmd.Parameters.Add(new SqlParameter("@EdgeTypeID", null));
				cmd.Parameters.Add(new SqlParameter("@MeasureName", null));

				foreach (var obj in flatObjectList)
				{
					var dimension = obj as ObjectDimension;
					if (dimension != null && dimension.Value is ConstEdgeField)
					{
						cmd.Parameters["@EdgeFieldID"].Value = DBNull.Value;
						cmd.Parameters["@EdgeFieldName"].Value = (dimension.Value as ConstEdgeField).Name;
						cmd.Parameters["@EdgeTypeID"].Value = DBNull.Value;
						cmd.Parameters["@MeasureName"].Value = DBNull.Value;
						cmd.ExecuteNonQuery();
					}
					else if (dimension != null && dimension.Value is EdgeObject && dimension.Field != null)
					{
						// GK field
						cmd.Parameters["@EdgeFieldID"].Value = dimension.Field.FieldID;
						cmd.Parameters["@EdgeFieldName"].Value = String.Format("{0}_gk", dimension.Field.Name);
						cmd.Parameters["@EdgeTypeID"].Value = dimension.Field.FieldEdgeType.TypeID;
						cmd.Parameters["@MeasureName"].Value = DBNull.Value;
						cmd.ExecuteNonQuery();
						
						// type field
						cmd.Parameters["@EdgeFieldID"].Value = dimension.Field.FieldID;
						cmd.Parameters["@EdgeFieldName"].Value = String.Format("{0}_type", dimension.Field.Name);
						cmd.Parameters["@EdgeTypeID"].Value = DBNull.Value;
						cmd.Parameters["@MeasureName"].Value = DBNull.Value;
						cmd.ExecuteNonQuery();
					}
					else if (obj is KeyValuePair<Measure, double>)
					{
						cmd.Parameters["@EdgeFieldID"].Value = DBNull.Value;
						cmd.Parameters["@EdgeFieldName"].Value = DBNull.Value;
						cmd.Parameters["@EdgeTypeID"].Value = DBNull.Value;
						cmd.Parameters["@MeasureName"].Value = ((KeyValuePair<Measure, double>)obj).Key.Name;
						cmd.ExecuteNonQuery();
					}
				}
			}
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

			using (var command = new SqlCommand())
			{
				foreach (var column in columnList)
				{
					columnsStr = String.Format("{0}\n{1},", columnsStr, column.Name);
					valuesStr = String.Format("{0}\n@{1},", valuesStr, column.Name);
					command.Parameters.Add(new SqlParameter(String.Format("@{0}", column.Name), column.Value));
				}

				// remove last commas
				if (columnsStr.Length > 1) columnsStr = columnsStr.Remove(columnsStr.Length - 1, 1);
				if (valuesStr.Length > 1) valuesStr = valuesStr.Remove(valuesStr.Length - 1, 1);

				// prepare and execute INSERT
				command.Connection = _deliverySqlConnection;
				command.CommandText = String.Format("INSERT INTO {0} ({1}) VALUES ({2});", TableName, columnsStr, valuesStr);
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
				var dimension = obj as ObjectDimension;
				if (dimension != null && dimension.Value is EdgeObject)
				{
					var edgeObj = dimension.Value as EdgeObject;

					if(dimension.Field == null)
						throw new ConfigurationErrorsException(String.Format("Dimention does not contain field definition of edge type {0}", edgeObj.EdgeType.Name));

					foreach (var column in CreateEdgeObjColumns(edgeObj, dimension.Field.Name).Where(column => !columnDict.ContainsKey(column.Name)))
					{
						columnDict.Add(column.Name, column);
					}
					
					// EdgeObject are added to cache in order to insert them later into object tables
					if (addToCache)
						_edgeObjectsManger.AddToCache(edgeObj);
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
		/// <param name="columnName"></param>
		/// <returns></returns>
		private IEnumerable<Column> CreateEdgeObjColumns(EdgeObject obj, string columnName = "")
		{
			var columnList = new List<Column>();

			if (obj.EdgeType == null)
				throw new ConfigurationErrorsException(String.Format("EdgeType is not set for object {0}", obj.GetType()));

			// if column name is not set according to EdgeField set it according to EdgeType
			columnName = String.IsNullOrEmpty(columnName) ? obj.EdgeType.Name : columnName;

			// add 3 columns: GK (to be set later in Identify stage), TK (temp key) and type (from EdgeTypes)
			columnList.Add(new Column { Name = String.Format("{0}_gk", columnName), Value = obj.GK });
			columnList.Add(new Column { Name = String.Format("{0}_tk", columnName), Value = obj.TK, DbType = SqlDbType.NVarChar, Size = 500 });
			columnList.Add(new Column { Name = String.Format("{0}_type", columnName), Value = obj.EdgeType.TypeID, DbType = SqlDbType.Int });

			return columnList;
		}

		/// <summary>
		/// Create column from object accoding to the object type
		/// </summary>
		/// <param name="obj"></param>
		/// <returns></returns>
		private Column CreateColumn(object obj)
		{
			string clmName = null;
			object clmValue = null;
			var isNullable = false;
			var clmType = SqlDbType.BigInt;

			if (obj is KeyValuePair<Measure, double>)
			{
				var measure = (KeyValuePair<Measure, double>)obj;
				clmName = measure.Key.DataType == MeasureDataType.Currency ? string.Format("{0}_Converted", measure.Key.Name) : measure.Key.Name;
				clmValue = measure.Value;
				clmType = SqlDbType.Float;
			}
			else if (obj is ObjectDimension)
			{
				var field = (obj as ObjectDimension).Value as ConstEdgeField;
				if (field != null)
				{
					clmName = field.Name;
					clmValue = field.Value;
					clmType = Convert2DbType(field.Type);
					isNullable = true;
				}
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

		/// <summary>
		/// Search for best match table in staging DB according to delivery table structure
		/// </summary>
		/// <returns></returns>
		public string FindStagingTable()
		{
			using (var cmd = SqlUtility.CreateCommand(SP_FIND_BEST_MATCH_METRICS_TABLE, CommandType.StoredProcedure))
			{
				cmd.Connection = _deliverySqlConnection;
				var tableNameParam = new SqlParameter { ParameterName = "@BestMatch", Size = 1000, Direction = ParameterDirection.Output };
				cmd.Parameters.Add(tableNameParam);
				cmd.Parameters.AddWithValue("@InputTable", TableName);
				cmd.ExecuteNonQuery();

				if (tableNameParam.Value == null || String.IsNullOrEmpty(tableNameParam.Value.ToString()))
					throw new Exception(String.Format("No staging table was found for delivery table {0}", TableName));

				return tableNameParam.Value.ToString();
			}
		}

		/// <summary>
		/// Call stored procedure to insert delivery metrics data from delivery table into staging table
		/// </summary>
		/// <param name="deliveryTableName"></param>
		/// <param name="stagingTableName"></param>
		public void Stage(string deliveryTableName, string stagingTableName)
		{
			using (var cmd = SqlUtility.CreateCommand(SP_STAGE_DELIVERY_METRICS, CommandType.StoredProcedure))
			{
				cmd.Connection = _deliverySqlConnection;
				cmd.Parameters.AddWithValue("@FromTable", deliveryTableName);
				cmd.Parameters.AddWithValue("@ToTable", stagingTableName);
				cmd.ExecuteNonQuery();
			}
		} 
		#endregion
	}
}
