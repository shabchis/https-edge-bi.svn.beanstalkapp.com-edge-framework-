using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using Edge.Core.Utilities;
using Edge.Data.Objects;
using Edge.Data.Pipeline.Objects;
using System.Configuration;

namespace Edge.Data.Pipeline.Metrics.Managers
{
	/// <summary>
	/// Handle EdgeObject operations:
	/// 1. Create delivery tables for EdgeObject
	/// 2. Contains cache of delivery objects and Import them into EdgeObject delivery tables
	/// 3. Supply flat object list from Metrics 
	/// </summary>
	internal class EdgeObjectsManager
	{
		#region Data Members

		private readonly SqlConnection _deliverySqlConnection;
		private readonly SqlConnection _objectsSqlConnection;

		// dictionary of objects per level of hierarchy in MetricsUnit
		private readonly Dictionary<int, List<object>> _objectsByLevel = new Dictionary<int, List<object>>();

		// dictionary of objects by TK (temporary key)
		private readonly Dictionary<string, EdgeObject> _objectsCache = new Dictionary<string, EdgeObject>();
		
		#endregion

		#region Ctor

		public EdgeObjectsManager(SqlConnection deliveryConnection, SqlConnection objectsConnectin)
		{
			_deliverySqlConnection = deliveryConnection;
			_objectsSqlConnection = objectsConnectin;
		}
		#endregion

		#region Properties

		public Dictionary<string, EdgeType> EdgeTypes { get; set; }
		
		#endregion

		#region Indexer

		private List<object> this[int index]
		{
			get { return _objectsByLevel[index]; }
		}

		#endregion

		#region Import
		#region Public Methods

		/// <summary>
		/// Call DB stored procedure to create all delivery object tables by table prefix
		/// currently all delivery objects tables are created for all accounts even if they are not in use
		/// </summary>
		/// <param name="tablePrefix"></param>
		public void CreateDeliveryObjectTables(string tablePrefix)
		{
			using (var cmd = SqlUtility.CreateCommand("MD_ObjectTables_Create", CommandType.StoredProcedure))
			{
				cmd.Parameters.AddWithValue("@TablePrefix", string.Format("{0}_", tablePrefix));
				cmd.Connection = _objectsSqlConnection;

				cmd.CommandTimeout = 80; //DEFAULT IS 30 AND SOMTIME NOT ENOUGH WHEN RUNING CUBE
				cmd.ExecuteNonQuery();
			}
		}

		/// <summary>
		/// Insert objects into object DB tables
		/// </summary>
		public void ImportObjects(string tablePrefix)
		{
			foreach (var obj in _objectsCache)
			{
				var columns = String.Empty;
				var values = String.Empty;
				var paramList = new List<SqlParameter>();

				// GK and TK
				AddColumn(ref columns, ref values, paramList, "GK", obj.Value.GK);
				AddColumn(ref columns, ref values, paramList, "TK", obj.Value.TK);

				// account
				AddColumn(ref columns, ref values, paramList, "AccountID", obj.Value.Account.ID);

				// type ID
				AddColumn(ref columns, ref values, paramList, "TypeID", obj.Value.EdgeType.TypeID);

				// specific fields by object type
				BuildSpecificFields(obj.Value, ref columns, ref values, paramList);

				// fields defined in object and configured to be stored (according to MD_EdgeField)
				BuildEdgeObjectFields(obj.Value, ref columns, ref values, paramList);

				var insertSql = String.Format("INSERT INTO [DBO].[{0}_{1}] \n({2}) \nVALUES \n({3})", tablePrefix, obj.Value.EdgeType.TableName, columns, values);
				using (var command = new SqlCommand(insertSql, _deliverySqlConnection))
				{
					command.Parameters.AddRange(paramList.ToArray());
					command.ExecuteNonQuery();
				}
			}
		}

		/// <summary>
		/// Add EdgeObject to cache, which contains distinct EdgeObjects,
		/// in order to import EdgeObjects into EdgeObjects delivery tables when import metrics finishes 
		/// </summary>
		/// <param name="obj"></param>
		public void AddToCache(EdgeObject obj)
		{
			var key = String.Format("{0}_{1}", obj.EdgeType.Name, obj.TK);
			if (!_objectsCache.ContainsKey(key))
			{
				_objectsCache.Add(key, obj);
			}
		}

		/// <summary>
		/// Create flat list of objects which compose metrics data object
		/// </summary>
		/// <param name="metricsUnit"></param>
		/// <returns></returns>
		public List<object> GetFlatObjectList(MetricsUnit metricsUnit)
		{
			// clear all containers
			ClearObjects();

			var level = 0;
			var flatList = new List<object>();

			// add all metrics dimensions to object structure
			foreach (var dimension in metricsUnit.GetObjectDimensions().Where(dimension => dimension != null))
			{
				Add(dimension, level);
			}

			// go over object composition levels and add objects to flat list
			while (ContainsKey(level) && this[level] != null && this[level].Count > 0)
			{
				// add all objects of this level to flat list (check if it not exists already)
				foreach (var obj in this[level])
				{
					var objDim = obj as ObjectDimension;
					if (objDim == null) continue;

					if (objDim.Field == null || !flatList.Any(  x => x is ObjectDimension && 
															  ((x as ObjectDimension).Field == objDim.Field ||	// check if object already added by field definition
															   (x as ObjectDimension).Value == objDim.Value)	// check if object already added by object value 
															 ))													// (for example creative ref in creative definition should not be added, already added in composite creative)
						flatList.Add(obj);
				}

				foreach (var obj in this[level])
				{
					var dimension = obj as ObjectDimension;
					if (dimension != null && dimension.Value is EdgeObject)
					{
						// add all dimensions of the EdgeObject
						foreach (var childDimension in (dimension.Value as EdgeObject).GetObjectDimensions())
						{
							Add(childDimension, level + 1);
						}
					}
				}
				level++;
			}

			// add measures
			if (metricsUnit.MeasureValues != null)
			{
				flatList.AddRange(metricsUnit.MeasureValues.Cast<object>());
			}

			return Normalize(flatList, metricsUnit);
		}

		#endregion

		#region Private Methods
		private void Add(object obj, int level)
		{
			if (!_objectsByLevel.ContainsKey(level))
				_objectsByLevel.Add(level, new List<object>());

			_objectsByLevel[level].Add(obj);
		}

		private bool ContainsKey(int level)
		{
			return _objectsByLevel.ContainsKey(level);
		}

		/// <summary>
		/// Get EdgeObject fields which are configured in MD_EdgeField table according to object type
		/// get fields values by reflection and compose values and columns string for INSERT
		/// </summary>
		/// <param name="edgeObject"></param>
		/// <param name="columns"></param>
		/// <param name="values"></param>
		/// <param name="paramList"></param>
		private void BuildEdgeObjectFields(EdgeObject edgeObject, ref string columns, ref string values, ICollection<SqlParameter> paramList)
		{
			// get the list of configured fields in MD_EdgeFields according to the object type
			foreach (var field in edgeObject.EdgeType.Fields)
			{
				// add extra field column
				var edgeFieldObj = edgeObject.GetObjectDimensions().Where(x => x.Field == field.Field).Select(x => x.Value).FirstOrDefault() as EdgeObject;
				if (edgeFieldObj != null)
				{
					AddColumn(ref columns, ref values, paramList, String.Format("{0}_gk", field.ColumnName), edgeFieldObj.GK);
					AddColumn(ref columns, ref values, paramList, String.Format("{0}_tk", field.ColumnName), edgeFieldObj.TK);
					AddColumn(ref columns, ref values, paramList, String.Format("{0}_type", field.ColumnName), edgeFieldObj.EdgeType.TypeID);
				}
				else
				{
					// try to get field by reflection by configured edge field name
					var member = edgeObject.GetType().GetMember(field.Field.Name);
					if (member.Length > 0)
					{
						var memberInfo = edgeObject.GetType().GetMember(field.Field.Name)[0];
						var value = (memberInfo is FieldInfo ? (memberInfo as FieldInfo).GetValue(edgeObject) : null) ??
									(memberInfo is PropertyInfo ? (memberInfo as PropertyInfo).GetValue(edgeObject, null) : null);

						var type = memberInfo is FieldInfo
										? (memberInfo as FieldInfo).FieldType
										: memberInfo is PropertyInfo ? (memberInfo as PropertyInfo).PropertyType : null;

						// edge object
						if (value is EdgeObject)
						{
							var edgeObj = value as EdgeObject;
							AddColumn(ref columns, ref values, paramList, String.Format("{0}_gk", field.ColumnName), edgeObj.GK);
							AddColumn(ref columns, ref values, paramList, String.Format("{0}_tk", field.ColumnName), edgeObj.TK);
							AddColumn(ref columns, ref values, paramList, String.Format("{0}_type", field.ColumnName), edgeObj.EdgeType.TypeID);
						}
						else if (value != null)  // primitive types
						{
							// special case for enum value - parse to INT
							if (type.BaseType == typeof(Enum))
							{
								value = (int)Enum.Parse(type, value.ToString());
							}
							AddColumn(ref columns, ref values, paramList, field.ColumnName, value);
						}
					}
				}
			}
		}

		/// <summary>
		/// Handle specific fields of th EdgeObject which are defined hard coded in Object table 
		/// </summary>
		/// <param name="edgeObject"></param>
		/// <param name="columns"></param>
		/// <param name="values"></param>
		/// <param name="paramList"></param>
		private void BuildSpecificFields(EdgeObject edgeObject, ref string columns, ref string values, ICollection<SqlParameter> paramList)
		{
			if (edgeObject is Ad)
			{
				// destination Url
				AddColumn(ref columns, ref values, paramList, "DestinationUrl", (edgeObject as Ad).DestinationUrl);
			}
			else if (edgeObject is CreativeDefinition)
			{
				// destination Url
				AddColumn(ref columns, ref values, paramList, "DestinationUrl", (edgeObject as CreativeDefinition).DestinationUrl);
			}

			// fields defined for channel specific objects
			if (edgeObject is ChannelSpecificObject)
			{
				var channelObj = edgeObject as ChannelSpecificObject;
				AddColumn(ref columns, ref values, paramList, "ChannelID", channelObj.Channel.ID);
				AddColumn(ref columns, ref values, paramList, "OriginalID", channelObj.OriginalID);
				AddColumn(ref columns, ref values, paramList, "Status", (int)channelObj.Status);
			}
		}

		/// <summary>
		/// Normalize flat object list to set:
		/// Account and Channel to all objects according to Metrics unit definition
		/// </summary>
		/// <param name="flatObjectList"></param>
		/// <param name="metricsUnit"></param>
		/// <returns></returns>
		private List<object> Normalize(List<object> flatObjectList, MetricsUnit metricsUnit)
		{
			foreach (var obj in flatObjectList)
			{
				// edge object can be an object or an extra field
				var edgeObj = obj as EdgeObject;
				if (obj is ObjectDimension && (obj as ObjectDimension).Value is EdgeObject)
				{
					edgeObj = (obj as ObjectDimension).Value as EdgeObject;
				}
				if (edgeObj != null)
				{
					// set account and channeL
					edgeObj.Account = metricsUnit.Account;
					if (edgeObj is ChannelSpecificObject)
					{
						(edgeObj as ChannelSpecificObject).Channel = metricsUnit.Channel;
					}
				}
			}
			return flatObjectList;
		}

		private void ClearObjects()
		{
			_objectsByLevel.Clear();
		}

		/// <summary>
		/// Add column to columns string, valuses string and Sql parameters list
		/// </summary>
		/// <param name="columns"></param>
		/// <param name="values"></param>
		/// <param name="paramList"></param>
		/// <param name="columnName"></param>
		/// <param name="value"></param>
		private void AddColumn(ref string columns, ref string values, ICollection<SqlParameter> paramList, string columnName, object value)
		{
			if (value != null)
			{
				columns = String.Format("{0}{1}", !String.IsNullOrEmpty(columns) ? String.Format("{0},\n", columns) : columns, columnName);
				values = String.Format("{0}@{1}", !String.IsNullOrEmpty(values) ? String.Format("{0},\n", values) : values, columnName);
				paramList.Add(new SqlParameter(String.Format("@{0}", columnName), value));
			}
		}
		#endregion 
		#endregion
	}
}
