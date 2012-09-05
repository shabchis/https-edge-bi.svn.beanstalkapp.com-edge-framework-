using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Data;
using Edge.Data.Pipeline.Metrics;
using System.Data.SqlClient;
using Edge.Core.Configuration;
using Edge.Core.Utilities;


namespace Edge.Data.Objects
{
	internal class TableManager
	{
		string _tablePrefix;
		SqlConnection _sqlConnection;
		public TableManager(SqlConnection connection)
		{
			_sqlConnection = connection;
		}
		public class Column
		{
			public string Name { get; set; }
			public SqlDbType DbType { get; set; }
			public int Size { get; set; }
			public object Value { get; set; }
			public bool Nullable { get; set; }
			public string DefaultValue { get; set; }
		}

		Dictionary<string, Column> _cols = new Dictionary<string, Column>();
		EdgeObjectsManager _objectsManger = new EdgeObjectsManager();
		public string CreateDeliveryMetricsTable(string tablePerifx, MetricsUnit metricsUnit)
		{
			_tablePrefix = tablePerifx;
			string tableName;
			#region looping object in predetermined order-
			int pass = 0;
			if (metricsUnit is AdMetricsUnit)
			{
				AdMetricsUnit adMetricsUnit = (AdMetricsUnit)metricsUnit;
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

			#region createMetricsTable

			StringBuilder builder = new StringBuilder();
			tableName = string.Format("{0}_Metrics", this._tablePrefix);
			builder.AppendFormat("create table [dbo].{0}(\n", tableName);
			foreach (Column col in _cols.Values)
			{

				builder.AppendFormat("\t[{0}] [{1}] {2} {3} {4}, \n",
					col.Name,
					col.DbType,
					col.Size != 0 ? string.Format("({0})", col.Size) : null,
					col.Nullable ? "null" : "not null",
					col.DefaultValue != string.Empty ? string.Format("Default {0}", col.DefaultValue) : string.Empty
				);
			}
			builder.Remove(builder.Length - 1, 1);
			builder.Append(");");
			using (SqlCommand command = new SqlCommand(builder.ToString(), _sqlConnection))
			{
				command.CommandTimeout = 80; //DEFAULT IS 30 AND SOMTIME NOT ENOUGH WHEN RUNING CUBE
				command.ExecuteNonQuery();

			}

			#endregion
			return tableName;
		}
		private void AddColumn(object obj)
		{
			string typeName = obj.GetType().Name;
			string edgeobjectsSuffix = "Usid";
			string colName;
			if (obj.GetType().IsSubclassOf(typeof(EdgeObject)))
			{
				switch (typeName)
				{
					case "Ad":
						{
							colName = string.Format("{0}_{1}", typeName, edgeobjectsSuffix);
							Ad ad = (Ad)obj;
							if (!_cols.ContainsKey(colName))
							{

								_cols.Add(colName, new Column() { Name = colName, Value = ad.GK });
							}
							break;
						}
					case "CompositeCreative":
						{
							colName = string.Format("{0}_{1}", typeName, edgeobjectsSuffix);
							CompositeCreative composite = (CompositeCreative)obj;
							if (!_cols.ContainsKey(colName))
								_cols.Add(colName, new Column() { Name = colName });
							var childCreatives = composite.ChildCreatives.OrderBy(p => p.Key);


							foreach (var childCreative in childCreatives)
							{
								colName = string.Format("{0}_{1}", childCreative.Key, edgeobjectsSuffix);
								if (!_cols.ContainsKey(colName))
									_cols.Add(colName, new Column() { Name = colName, Value = childCreative.Value.GK });

							}

							break;
						}
					case "SingleCreative":
						{
							colName = string.Format("{0}_{1}", typeName, edgeobjectsSuffix);
							if (!_cols.ContainsKey(colName))
							{
								Creative creative = (Creative)obj;
								_cols.Add(colName, new Column() { Name = colName, Value = creative.GK });
							}
							break;
						}
					default:
						{
							if (obj is Target)
							{
								int i = 2;
								colName = string.Format("{0}_{1}", typeName, edgeobjectsSuffix);

								while (_cols.ContainsKey(colName))
								{
									colName = string.Format("{0}{1}_{2}", typeName, i, edgeobjectsSuffix);
									i++;
								}
								Target target = (Target)obj;
								_cols.Add(colName, new Column() { Name = colName, Value = target.GK });
							}
							break;
						}
				}
			}
			else
			{
				Type t = obj.GetType();

				if (t == typeof(KeyValuePair<MetaProperty, object>))
				{
					KeyValuePair<MetaProperty, object> metaProperty = (KeyValuePair<MetaProperty, object>)obj;
					if (!_cols.ContainsKey(metaProperty.Key.PropertyName))
					{
						_cols.Add(metaProperty.Key.PropertyName, new Column() { Name = metaProperty.Key.PropertyName });
					}

				}
				else if (t == typeof(KeyValuePair<Measure, double>))
				{
					KeyValuePair<Measure, double> measure = (KeyValuePair<Measure, double>)obj;
					string name = measure.Key.DataType == MeasureDataType.Currency ? string.Format("{0}_Converted", measure.Key.Name) : measure.Key.Name;
					if (!_cols.ContainsKey(name))
					{
						_cols.Add(name, new Column() { Name = name, Value = measure.Value });
					}

				}
			}
		}
		private void AddObjects(EdgeObject obj)
		{


			if (obj.MetaProperties != null)
			{
				foreach (KeyValuePair<MetaProperty, object> metaProperty in obj.MetaProperties)
				{
					if (metaProperty.Value.GetType() != typeof(EdgeObject))
					{
						AddColumn(metaProperty);
					}
					else
					{
						if (!_objectsManger.ContainsKey((EdgeObject)metaProperty.Value))
							_objectsManger.Add((EdgeObject)metaProperty.Value);
					}
				}

			}

		}


		public string FindStagingTable(string metricsTableName)
		{
			string stagingTableName;
			using (SqlCommand command = SqlUtility.CreateCommand(AppSettings.Get(this, "SP_FindStagingTable"), CommandType.StoredProcedure))
			{
				command.Parameters["@templateTable"].Value = metricsTableName;
				command.Parameters["@templateDB"].Value = "";//TODO: FROM WHERE DO i TAKE THIS TABLE?
				command.Parameters["@searchDB"].Value = "";//TODO: FROM WHERE DO i TAKE THIS TABLE?
				using (SqlDataReader reader = command.ExecuteReader())
				{
					if (!reader.Read())
						throw new Exception("No staging table   Found");
					else
						stagingTableName = reader["TABLE_NAME"].ToString();

				}


			}
			return stagingTableName;
		}
	}
	public class EdgeObjectsManager
	{
		Dictionary<EdgeObject, EdgeObject> _allObjects = new Dictionary<EdgeObject, EdgeObject>();
		Dictionary<int, List<EdgeObject>> _objectsByPass = new Dictionary<int, List<EdgeObject>>();
		Dictionary<EdgeObject, EdgeObject> _otherObjects = new Dictionary<EdgeObject, EdgeObject>();
		public List<EdgeObject> this[int index]
		{
			get
			{
				return _objectsByPass[index];
			}

		}
		public void Add(EdgeObject obj, int pass)
		{


			if (!_objectsByPass.ContainsKey(pass))
				_objectsByPass.Add(pass, new List<EdgeObject>());
			_objectsByPass[pass].Add(obj);

		}
		public void Add(EdgeObject obj)
		{
			if (_allObjects.ContainsKey(obj))
				throw new System.ArgumentException(string.Format("element {0} of type {1}  already exists in _allObjects dictionary", obj.Name, obj.GetType().Name));
			_otherObjects.Add(obj, obj);
			_allObjects.Add(obj, obj);
		}
		public bool ContainsKey(EdgeObject obj)
		{
			return _allObjects.ContainsKey(obj) ? true : false;
		}
		public bool ContainsKey(int pass)
		{
			return _objectsByPass.ContainsKey(pass) ? true : false;
		}
		public Dictionary<int, List<EdgeObject>>.ValueCollection ObjectsByPassValues()
		{
			return _objectsByPass.Values;
		}

		public Dictionary<EdgeObject, EdgeObject>.ValueCollection GetOtherObjects()
		{
			return _otherObjects.Values;
		}
	}
}
