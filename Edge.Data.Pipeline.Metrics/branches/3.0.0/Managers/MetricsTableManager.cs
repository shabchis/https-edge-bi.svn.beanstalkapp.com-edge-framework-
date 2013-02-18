using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Data;
using System.Data.SqlClient;
using Edge.Data.Objects;
using Edge.Data.Pipeline.Metrics.Misc;
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
		//private const string EGDE_OBJECTS_SUFFIX = "Usid";
		public string TableName { get; set; }

		readonly SqlConnection _sqlConnection;
		readonly EdgeObjectsManager _edgeObjectsManger;

		//readonly Dictionary<string, Column> _columns = new Dictionary<string, Column>();

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
		/// <param name="tablePrefix"></param>
		/// <param name="metricsUnit">sample metric unit for table structure</param>
		/// <returns></returns>
		public MetricsTableMetadata CreateDeliveryMetricsTable(string tablePrefix, MetricsUnit metricsUnit)
		{
			TableName = string.Format("[DBO].[{0}_Metrics]", tablePrefix);

			var flatObjectList = _edgeObjectsManger.GetFlatObjectList(metricsUnit);

			var columnList = GetColumnList(flatObjectList, false); // sampe metrics objects are not added to object cache

			CreateTable(columnList);

			// TODO: find hte right place to call this method
			//_edgeObjectsManger.BuildMetricDependencies(flatObjectList);

			return GetTableMetadata(flatObjectList, metricsUnit);
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
			using (var command = new SqlCommand(builder.ToString(), _sqlConnection))
			{
				command.CommandTimeout = 80; //DEFAULT IS 30 AND SOMTIME NOT ENOUGH WHEN RUNING CUBE
				command.ExecuteNonQuery();
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
				command.Connection = _sqlConnection;
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
					var columnName = dimension.Field != null ? dimension.Field.Name : edgeObj.EdgeType.Name;

					foreach (var column in CreateEdgeObjColumns(edgeObj, columnName).Where(column => !columnDict.ContainsKey(column.Name)))
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

		private MetricsTableMetadata GetTableMetadata(IEnumerable<object> flatObjectList, MetricsUnit metricsUnit)
		{
			var tableMetadata = new MetricsTableMetadata {TableName = TableName};

			// add fields on metrics unit level
			foreach (var dimension in metricsUnit.GetObjectDimensions())
			{
				if (dimension.Value is EdgeObject)
					tableMetadata.FieldList.Add(new FieldMetadata {Field = dimension.Field});
			}

			// add fields deeper in metrics unit according to the flat object list
			foreach (var obj in flatObjectList)
			{
				if (obj is ObjectDimension && (obj as ObjectDimension).Value is EdgeObject)
				{
					var edgeObject = (obj as ObjectDimension).Value as EdgeObject;
					if (edgeObject != null && edgeObject.EdgeType != null && edgeObject.EdgeType.Fields != null)
					{
						foreach (var field in edgeObject.EdgeType.Fields)
						{
							tableMetadata.FieldList.Add(new FieldMetadata {Field = field});
						}
					}
				}
				else if (obj is KeyValuePair<Measure, double>)
				{
					var measure = (KeyValuePair<Measure, double>)obj;
					tableMetadata.FieldList.Add(new FieldMetadata { Field = measure.Key });
				}
			}
			return tableMetadata;
		}

		#endregion

		#region Staging
		/// <summary>
		/// Search for best match table in staging DB according to delivery table structure
		/// </summary>
		/// <param name="deliveryTableName"></param>
		/// <returns></returns>
		public string FindStagingTable(string deliveryTableName)
		{
			string stagingTableName = String.Empty;
			// call stored procedure to find best mathed table for staging
			//using (var command = SqlUtility.CreateCommand(AppSettings.Get(this, "SP_FindStagingTable"), CommandType.StoredProcedure))
			//{
			//	command.Parameters["@templateTable"].Value = deliveryTableName;
			//	command.Parameters["@templateDB"].Value = ""; //TODO: FROM WHERE DO i TAKE THIS TABLE?
			//	command.Parameters["@searchDB"].Value = ""; //TODO: FROM WHERE DO i TAKE THIS TABLE?

			//	using (var reader = command.ExecuteReader())
			//	{
			//		if (!reader.Read())
			//			throw new Exception(String.Format("No staging table was found for delivery table {0}", deliveryTableName));
					
			//		stagingTableName = reader["TABLE_NAME"].ToString();
			//	}
			//}
			return stagingTableName;
		}

		public void Staging(string deliveryTable, string stagingTable)
		{
			// TODO: should only call store procedure to copy data from delivery to staging
			//List<Column> cols = _columns.Values.ToList();
			//var builder = new StringBuilder();
			//builder.AppendFormat("INSERT INTO {0}\n(", stagingTable);
			//for (int i = 0; i < cols.Count; i++)
			//{
			//	builder.AppendFormat(i != cols.Count - 1 ? "\t{0},\n" : "\t{0})\n", cols[i].Name);
			//}
			//builder.Append("\tVALUES (SELECT\n");
			//for (int i = 0; i < cols.Count; i++)
			//{
			//	string colName = cols[i].Name.Contains(EGDE_OBJECTS_SUFFIX) ? "GKS.GK" : string.Format("{0}.{1}", "Metrics", cols[i].Name);

			//	builder.AppendFormat(i != cols.Count - 1 ? "\t{0},\n" : "\t{0})\n", colName);
			//}

			//builder.Append("\tWHERE (\n");
			//bool firstFilter = true;
			//for (int i = 0; i < cols.Count; i++)
			//{
			//	string filter = string.Empty;
			//	if (cols[i].Name.Contains(EGDE_OBJECTS_SUFFIX))
			//	{
			//		if (firstFilter)
			//		{
			//			filter = string.Format("Metrics.{0}=GKS.Usid\n", cols[i].Name);
			//			firstFilter = false;
			//		}
			//		else
			//			filter = string.Format("AND Metrics.{0}=GKS.Usid\n", cols[i].Name);
			//	}

			//	if (i != cols.Count - 1)
			//	{
			//		filter = string.IsNullOrEmpty(filter) ? "\t)" : string.Format("\t{0})\n", filter);
			//	}

			//	if (!string.IsNullOrEmpty(filter))
			//		builder.Append(filter);

			//}
			//using (var command = new SqlCommand(builder.ToString(), _sqlConnection))
			//{
			//	command.ExecuteNonQuery();
			//}
		} 
		#endregion
	}
}
