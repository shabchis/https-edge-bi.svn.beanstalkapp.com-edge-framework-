using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Data.SqlClient;
using Edge.Core.Configuration;
using Edge.Core.Utilities;
using Edge.Data.Objects;
using Edge.Data.Pipeline.Objects;

namespace Edge.Data.Pipeline.Metrics.Base.Submanagers
{
	/// <summary>
	/// Table manager class is used to create Delivery metrics table and find matching table for Staging
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
		/// <summary>
		/// Create delivery metric table named by table prefix using sample metric unit structure
		/// </summary>
		/// <param name="tablePerifx">delivery metric table prefix</param>
		/// <param name="metricsUnit">sample metric unit for table structure</param>
		/// <returns></returns>
		public string CreateDeliveryMetricsTable(string tablePerifx, MetricsUnit metricsUnit)
		{
			_tablePrefix = tablePerifx;

			#region looping object in predetermined order
			var pass = 0;

			foreach (var dimention	in metricsUnit.GetObjectDimensions())
			{
				if (dimention != null)
					_edgeObjectsManger.Add(dimention, pass);
			}
			//if (metricsUnit is AdMetricsUnit)
			//{
			//	var adMetricsUnit = metricsUnit as AdMetricsUnit;
			//	_edgeObjectsManger.Add(adMetricsUnit.Ad, pass);
			//	_edgeObjectsManger.Add(adMetricsUnit.Ad.CreativeDefinition, pass);
			//}
			//foreach (var target in metricsUnit.TargetDimensions)
			//{
			//	_edgeObjectsManger.Add(target, pass);
			//}

			while (_edgeObjectsManger.ContainsKey(pass) && _edgeObjectsManger[pass] != null && _edgeObjectsManger[pass].Count > 0)
			{
				foreach (var obj in _edgeObjectsManger[pass])
				{
					AddColumn(obj);
				}

				foreach (var obj in _edgeObjectsManger[pass])
				{
					foreach (var field in obj.GetType().GetFields())
					{
						if (field.FieldType.IsSubclassOf(typeof(EdgeObject)))
						{
							_edgeObjectsManger.Add((EdgeObject)field.GetValue(obj), pass + 1);
						}
					}
				}
				pass++;
			}
			#endregion

			#region runing deeper on all edgeobjects and relevant properties
			foreach (List<object> edgeObjects in _edgeObjectsManger.ObjectsByPassValues())
			{
				foreach (var edgeObject in edgeObjects)
				{
					if (edgeObject is EdgeObject)
						AddObjects(edgeObject as EdgeObject);
				}
			}
			foreach (EdgeObject edgeObject in _edgeObjectsManger.GetOtherObjects())
			{
				AddColumn(edgeObject);
			}
			#endregion

			#region runing on measures
			if (metricsUnit.MeasureValues != null)
			{
				foreach (KeyValuePair<Measure, double> measure in metricsUnit.MeasureValues)
				{
					AddColumn(measure);
				}
			}
			#endregion

			#region Create Metrics Table

			var builder = new StringBuilder();
			var tableName = string.Format("{0}_Metrics", _tablePrefix);
			builder.AppendFormat("create table [dbo].[{0}](\n", tableName);
			foreach (Column col in _columns.Values)
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

			#endregion

			return tableName;
		}
		private void AddColumn(object obj)
		{
			if (obj is EdgeObject)
			{
				string colName = obj.GetType().Name;
				if (!_columns.ContainsKey(colName))
				{
					_columns.Add(colName, new Column { Name = colName, Value = ((EdgeObject)obj).GK });
					if (!((EdgeObject)obj).HasChildsObjects)
						return;
					foreach (var child in ((EdgeObject)obj).GetChildObjects())
					{
						AddColumn(child);
					}
				}
			}
			else
			{
				Type t = obj.GetType();

				if (t == typeof(KeyValuePair<EdgeField, object>))
				{
					var connection = (KeyValuePair<EdgeField, object>)obj;
					if (!_columns.ContainsKey(connection.Key.Name))
					{
						_columns.Add(connection.Key.Name, new Column { Name = connection.Key.Name });
					}
				}
				else if (t == typeof(KeyValuePair<Measure, double>))
				{
					var measure = (KeyValuePair<Measure, double>)obj;
					string name = measure.Key.DataType == MeasureDataType.Currency ? string.Format("{0}_Converted", measure.Key.Name) : measure.Key.Name;
					if (!_columns.ContainsKey(name))
					{
						_columns.Add(name, new Column { Name = name, Value = measure.Value });
					}
				}
				else if (t == typeof (ConstEdgeField))
				{
					var field = obj as ConstEdgeField;
					if (field != null && !_columns.ContainsKey(field.Name))
					{
						_columns.Add(field.Name, new Column { Name = field.Name, Value = field.Value, DbType = Convert2DBType(field.Type), Nullable = true});
					}
				}
			}
		}

		private SqlDbType Convert2DBType(Type type)
		{
			return  type == typeof(int)      ? SqlDbType.Int :
					type == typeof(Guid)     ? SqlDbType.VarChar :
					type == typeof(DateTime) ? SqlDbType.DateTime :
					type == typeof(double)   ? SqlDbType.Float :
					SqlDbType.NVarChar;
		}

		private void AddObjects(EdgeObject obj)
		{
			if (obj.ExtraFields != null)
			{
				foreach (KeyValuePair<ExtraField, object> metaProperty in obj.ExtraFields)
				{
					if (metaProperty.Value.GetType() != typeof(EdgeObject))
					{
						AddColumn(metaProperty);
					}
					else
					{
						if (!_edgeObjectsManger.ContainsKey(metaProperty.Value as EdgeObject))
							_edgeObjectsManger.Add(metaProperty.Value as EdgeObject);
					}
				}
			}
		}

		/// <summary>
		/// Save metrics row in DB
		/// </summary>
		/// <param name="metrics"></param>
		public void ImportMetrics(MetricsUnit metrics)
		{
			// meanwhile insert, later using ORM

			var columnsStr = String.Empty;
			var valuesStr = String.Empty;

			// add dimentions
			foreach (var dimention in metrics.GetObjectDimensions())
			{
				var column = GetColumn(dimention);
				columnsStr = String.Format("{0}\n{1},", columnsStr, column.Name);
				valuesStr = String.Format("{0}\n{1},", valuesStr, column.DbType == SqlDbType.NChar ? String.Format("'{0}'", column.Value) : 
																  column.DbType == SqlDbType.DateTime ? String.Format("'{0}'", ((DateTime)column.Value).ToString("yyyy-MM-dd HH:mm:ss")) :
																  column.Value);
			}
			// add meatures
			foreach (var measure in metrics.MeasureValues)
			{
				var column = GetColumn(measure);
				columnsStr = String.Format("{0}\n{1},", columnsStr, column.Name);
				valuesStr = String.Format("{0}\n{1},", valuesStr, column.Value);
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

		private Column GetColumn(object obj)
		{
			var clmName = obj.GetType().Name;
			object clmValue = null;
			Type cmlType = null;
			if (obj is EdgeObject)
			{
				clmValue = ((EdgeObject) obj).GK;
				
			}
			else if (obj is KeyValuePair<Measure, double>)
			{
				var measure = (KeyValuePair<Measure, double>)obj;
				clmName = measure.Key.DataType == MeasureDataType.Currency ? string.Format("{0}_Converted", measure.Key.Name) : measure.Key.Name;
				clmValue = measure.Value;
			}
			else if (obj is ConstEdgeField)
			{
				clmName = (obj as ConstEdgeField).Name;
				clmValue = (obj as ConstEdgeField).Value;
				cmlType = (obj as ConstEdgeField).Type;
			}
			return new Column {Name = clmName, Value = clmValue, DbType = cmlType != null ? Convert2DBType(cmlType) : SqlDbType.BigInt};

			// TODO - check what to do with the childs
			//if (!((EdgeObject)obj).HasChildsObjects)
			//	return;
			//foreach (var child in ((EdgeObject)obj).GetChildObjects())
			//{
			//	AddColumn(child);
			//}

			// TODO - check what to do with EdgeField
			//var t = obj.GetType();
			//if (t == typeof(KeyValuePair<EdgeField, object>))
			//{
			//	var connection = (KeyValuePair<EdgeField, object>)obj;
			//	if (!_columns.ContainsKey(connection.Key.Name))
			//	{
			//		_columns.Add(connection.Key.Name, new Column { Name = connection.Key.Name });
			//	}
			//}
				
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
						filter = string.Format("Metrics.{0}=GKS.Usid\n");
						firstFilter = false;
					}
					else
						filter = string.Format("AND Metrics.{0}=GKS.Usid\n");
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
