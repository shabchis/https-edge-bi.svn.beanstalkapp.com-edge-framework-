using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using Edge.Core.Configuration;
using Edge.Core.Utilities;
using Edge.Data.Objects;
using Edge.Data.Pipeline.Metrics.Misc;
using Edge.Data.Pipeline.Metrics.Services;
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

		// dictionary of objects per level of hierarchy in MetricsUnit
		private readonly Dictionary<int, List<object>> _objectsByLevel = new Dictionary<int, List<object>>();

		// dictionary of objects by TK (temporary key)
		private readonly Dictionary<string, EdgeObject> _objectsCache = new Dictionary<string, EdgeObject>();
		
		#endregion

		#region Ctor

		public EdgeObjectsManager(SqlConnection deliveryConnection)
		{
			_deliverySqlConnection = deliveryConnection;
		}
		#endregion

		#region Properties
		public List<ExtraField> ExtraFields { get; set; }
		public Dictionary<string, EdgeType> EdgeTypes { get; set; } 
		#endregion

		#region Indexer

		private List<object> this[int index]
		{
			get { return _objectsByLevel[index]; }
		}

		#endregion

		#region Public Methods

		/// <summary>
		/// Call DB stored procedure to create all delivery object tables by table prefix
		/// currently all delivery objects tables are created for all accounts even if they are not in use
		/// </summary>
		/// <param name="tablePrefix"></param>
		public void CreateDeliveryObjectTables(string tablePrefix)
		{
			using (var connection = new SqlConnection(AppSettings.GetConnectionString(typeof(MetricsProcessorServiceBase), Consts.ConnectionStrings.Objects)))
			{
				var cmd = SqlUtility.CreateCommand("MD_ObjectTables_Create", CommandType.StoredProcedure);
				cmd.Parameters.AddWithValue("@TablePrefix", string.Format("{0}_", tablePrefix));
				cmd.Connection = connection;
				connection.Open();

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

				// specific fields by object type
				BuildSpecificFields(obj.Value, ref columns, ref values, paramList);

				// fields defined in object and configured to be stored (according to MD_EdgeField)
				BuildEdgeObjectFields(obj.Value, ref columns, ref values, paramList);
				
				// extra fields
				BuildExtraFields4Sql(obj.Value, ref columns, ref values, paramList);

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

			// add all metrics dimentions to object structure
			foreach (var dimention in metricsUnit.GetObjectDimensions().Where(dimention => dimention != null))
			{
				Add(dimention, level);
			}

			// go over object composition levels and add objects to flat list
			while (ContainsKey(level) && this[level] != null && this[level].Count > 0)
			{
				// add all objects of this level
				flatList.AddRange(this[level].Where(obj => obj != null));

				foreach (var obj in this[level])
				{
					var dimention = obj as ObjectDimension;
					if (dimention != null && dimention.Value is EdgeObject)
					{
						// add all dimentions of the EdgeObject
						foreach (var dimentsion in (dimention.Value as EdgeObject).GetObjectDimensions())
						{
							Add(dimentsion, level + 1);
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

		private void BuildExtraFields4Sql(EdgeObject obj, ref string columns, ref string values, ICollection<SqlParameter> paramList)
		{
			if (obj.ExtraFields == null) return;

			foreach (var field in obj.ExtraFields)
			{
				if (field.Key.ColumnType == "obj")
				{
					var edgeObj = field.Value as EdgeObject;
					if (edgeObj != null)
					{
						AddColumn(ref columns, ref values, paramList, String.Format("{0}_Field{1}_GK", field.Key.ColumnType, field.Key.ColumnIndex), edgeObj.GK);
						AddColumn(ref columns, ref values, paramList, String.Format("{0}_Field{1}_TK", field.Key.ColumnType, field.Key.ColumnIndex), edgeObj.TK);
						AddColumn(ref columns, ref values, paramList, String.Format("{0}_Field{1}_type", field.Key.ColumnType, field.Key.ColumnIndex), field.Key.FieldEdgeType.TypeID);
					}
				}
				else // for all primitive types only value (INT, STRING, FLOAT, etc.)
				{
					AddColumn(ref columns, ref values, paramList, String.Format("{0}_Field{1}", field.Key.ColumnType, field.Key.ColumnIndex), field.Value);
				}
			}
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
			var fieldList = ExtraFields.Where(x => x.ParentEdgeType != null && x.ParentEdgeType.TypeID == edgeObject.EdgeType.TypeID);
			
			foreach (var field in fieldList)
			{
				// get field by reflection
				var memberInfo = edgeObject.GetType().GetMember(field.Name)[0];
				var fieldInfo = memberInfo as FieldInfo;
				if (fieldInfo != null)
				{
					var value = fieldInfo.GetValue(edgeObject);
					// special case for enum value - parse to INT
					if (fieldInfo.FieldType.BaseType == typeof (Enum))
					{
						value = (int)Enum.Parse(fieldInfo.FieldType, value.ToString());
					}
					AddColumn(ref columns, ref values, paramList, String.Format("{0}_Field{1}", field.ColumnType, field.ColumnIndex), value);
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
				var ad = edgeObject as Ad;
				// destination Url
				AddColumn(ref columns, ref values, paramList, "DestinationUrl", ad.DestinationUrl);

				// creative
				AddColumn(ref columns, ref values, paramList, "CreativeDefinitionGK", ad.CreativeDefinition.GK);
				AddColumn(ref columns, ref values, paramList, "CreativeDefinitionTK", ad.CreativeDefinition.TK);
				AddColumn(ref columns, ref values, paramList, "CreativeDefinitionTypeID", ad.CreativeDefinition.EdgeType.TypeID);
			}
			else if (edgeObject is CreativeDefinition)
			{
				var creativeDef = edgeObject as CreativeDefinition;
				// destination Url
				AddColumn(ref columns, ref values, paramList, "DestinationUrl", creativeDef.DestinationUrl);

				// creative
				//AddColumn(ref columns, ref values, paramList, "CreativeGK", creativeDef.Creative.GK);
				//AddColumn(ref columns, ref values, paramList, "CreativeTK", creativeDef.Creative.TK);
				//AddColumn(ref columns, ref values, paramList, "CreativeTypeID", creativeDef.Creative.EdgeType.TypeID);
			}
			else
			{
				// Type ID is defined in all objects except Ad
				AddColumn(ref columns, ref values, paramList, "TypeID", edgeObject.EdgeType.TypeID);
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
		/// 1. Account and Channel to all objects according to Metrics unit definition
		/// 2. EdgeType according to object type name or extra field name
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
					// if edge type is not set in configuration try to set it
					if (edgeObj.EdgeType == null)
					{
						// 1st - according to edge object name in EdgeType table
						var requiredTypeName = edgeObj.GetType().Name;
						if (EdgeTypes.ContainsKey(requiredTypeName))
						{
							edgeObj.EdgeType = EdgeTypes[requiredTypeName];
						}
						else
						{
							// 2nd - according to extra field name in EdgeField table (in this case, EdgeType Name = EdgeField Name)
							if (obj is ObjectDimension)
							{
								var dimension = (obj as ObjectDimension);
								if (EdgeTypes.ContainsKey(dimension.Field.Name))
								{
									edgeObj.EdgeType = EdgeTypes[dimension.Field.Name];
								}
								else
								{
									throw new ConfigurationErrorsException(String.Format("Edge type is not set for extra field {0} and cannot be found in EdgeTypes", dimension.Field.Name));
								}
							}
							else
							{
								throw new ConfigurationErrorsException(String.Format("Edge type is not set for object {0} and cannot be found in EdgeTypes by object name", requiredTypeName));
							}
						}
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
	}
}
