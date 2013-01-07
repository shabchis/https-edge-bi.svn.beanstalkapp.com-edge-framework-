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
	internal class TableManager
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
		string _tablePrefix;
		readonly SqlConnection _sqlConnection;
		private const string EGDE_OBJECTS_SUFFIX = "Usid";
		readonly Dictionary<string, Column> _columns = new Dictionary<string, Column>();
		readonly EdgeObjectsManager _objectsManger = new EdgeObjectsManager(); 
		#endregion

		#region Ctor
		public TableManager(SqlConnection connection)
		{
			_sqlConnection = connection;
		} 
		#endregion

		#region Delivery Metrics
		public string CreateDeliveryMetricsTable(string tablePerifx, MetricsUnit metricsUnit)
		{
			_tablePrefix = tablePerifx;

			#region looping object in predetermined order
			int pass = 0;
			if (metricsUnit is AdMetricsUnit)
			{
				var adMetricsUnit = metricsUnit as AdMetricsUnit;
				_objectsManger.Add(adMetricsUnit.Ad, pass);
				_objectsManger.Add(adMetricsUnit.Ad.Creative, pass);
			}
			foreach (var target in metricsUnit.TargetDimensions)
			{
				_objectsManger.Add(target, pass);
			}

			while (_objectsManger.ContainsKey(pass) && _objectsManger[pass] != null && _objectsManger[pass].Count > 0)
			{
				foreach (var obj in _objectsManger[pass])
				{
					AddColumn(obj);
				}

				foreach (var obj in _objectsManger[pass])
				{
					foreach (var field in obj.GetType().GetFields())
					{
						if (field.FieldType.IsSubclassOf(typeof(EdgeObject)))
						{
							_objectsManger.Add((EdgeObject)field.GetValue(obj), pass + 1);
						}
					}
				}
				pass++;
			}
			#endregion

			#region runing deeper on all edgeobjects and relevant properties
			foreach (List<EdgeObject> edgeObjects in _objectsManger.ObjectsByPassValues())
			{
				foreach (EdgeObject edgeObject in edgeObjects)
				{
					AddObjects(edgeObject);
				}
			}
			foreach (EdgeObject edgeObject in _objectsManger.GetOtherObjects())
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

				if (t == typeof(KeyValuePair<ConnectionDefinition, object>))
				{
					var connection = (KeyValuePair<ConnectionDefinition, object>)obj;
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
			}
		}
		private void AddObjects(EdgeObject obj)
		{
			if (obj.Connections != null)
			{
				foreach (KeyValuePair<ConnectionDefinition, EdgeObject> metaProperty in obj.Connections)
				{
					if (metaProperty.Value.GetType() != typeof(EdgeObject))
					{
						AddColumn(metaProperty);
					}
					else
					{
						if (!_objectsManger.ContainsKey(metaProperty.Value))
							_objectsManger.Add(metaProperty.Value);
					}
				}
			}
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
