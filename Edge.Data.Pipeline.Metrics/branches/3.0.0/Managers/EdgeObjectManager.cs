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

		private readonly Dictionary<EdgeObject, EdgeObject> _allObjects = new Dictionary<EdgeObject, EdgeObject>();
		private readonly Dictionary<int, List<object>> _objectsByPass = new Dictionary<int, List<object>>();
		private readonly Dictionary<EdgeObject, EdgeObject> _otherObjects = new Dictionary<EdgeObject, EdgeObject>();

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
			get { return _objectsByPass[index]; }
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
				// temporary key
				var columns = "TK";
				var values = String.Format("'{0}'", obj.Value.TK.RemoveInvalidCharacters());

				// global key
				columns = String.Format("{0},\nGK", columns);
				values = String.Format("{0},\n{1}", values, obj.Value.GK);

				// account
				columns = String.Format("{0},\nAccountID", columns);
				values = String.Format("{0},\n{1}", values, obj.Value.Account.ID);

				// specific fields by object type
				BuildSpecificFields(obj.Value, ref columns, ref values);

				// fields defined in object and configured to be stored (according to MD_EdgeField)
				BuildEdgeObjectFields(obj.Value, ref columns, ref values);
				
				// extra fields
				BuildExtraFields4Sql(obj.Value, ref columns, ref values);

				var insertSql = String.Format("INSERT INTO [DBO].[{0}_{1}] \n({2}) \nVALUES \n({3})", tablePrefix, obj.Value.EdgeType.TableName, columns, values);
				using (var command = new SqlCommand(insertSql, _deliverySqlConnection))
				{
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
			var key = String.Format("{0}_{1}", obj.GetType().Name, obj.TK);
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

				foreach (var obj in this[level].Where(obj => obj != null))
				{
					foreach (var field in obj.GetType().GetFields().Where(field => field.FieldType.IsSubclassOf(typeof(EdgeObject))))
					{
						Add(field.GetValue(obj), level + 1);
					}

					foreach (var prop in obj.GetType().GetProperties().Where(prop => prop.PropertyType.IsSubclassOf(typeof(EdgeObject))))
					{
						Add(prop.GetValue(obj, null), level + 1);
					}

					// handle dictionary of object fields
					foreach (var field in obj.GetType().GetFields().Where(field => field.FieldType.GetInterface("IDictionary`2") != null))
					{
						var dictionary = field.GetValue(obj);
						if (dictionary != null)
						{
							var keys = field.FieldType.GetInterface("IDictionary`2").GetProperty("Keys").GetValue(dictionary, null) as IEnumerable<CompositePartField>;

							if (keys != null)
							{
								foreach (var key in keys)
								{
									var val = field.FieldType.GetMethod("get_Item").Invoke(dictionary, new object[] {key}) as EdgeObject;
									var pair = new KeyValuePair<CompositePartField, EdgeObject>(key, val);
									Add(pair, level + 1);
								}
							}
						}
					}
				}
				level++;
			}

			// runing deeper on all Extra fields
			foreach (var edgeObjects in ObjectsByPassValues())
			{
				foreach (var obj in edgeObjects)
				{
					var edgeObj = obj as EdgeObject;
					if (edgeObj != null)
					{
						if (edgeObj.ExtraFields != null)
						{
							foreach (var metaProperty in edgeObj.ExtraFields)
							{
								if (metaProperty.Value.GetType() != typeof(EdgeObject))
								{
									flatList.Add(metaProperty);
								}
								else
								{
									Add(metaProperty.Value as EdgeObject);
								}
							}
						}
					}
				}
			}
			flatList.AddRange(GetOtherObjects());

			// add measures
			if (metricsUnit.MeasureValues != null)
			{
				flatList.AddRange(metricsUnit.MeasureValues.Cast<object>());
			}

			return Normalize(flatList, metricsUnit);
		}
		
		#endregion

		#region Private Methods
		private void Add(object obj, int pass)
		{
			if (!_objectsByPass.ContainsKey(pass))
				_objectsByPass.Add(pass, new List<object>());

			_objectsByPass[pass].Add(obj);
		}

		private void Add(EdgeObject obj)
		{
			if (_allObjects.ContainsKey(obj))
				throw new ArgumentException(string.Format("element {0} of type {1}  already exists in _allObjects dictionary", obj.Account.Name, obj.GetType().Name));

			_otherObjects.Add(obj, obj);
			_allObjects.Add(obj, obj);
		}
		
		private bool ContainsKey(int pass)
		{
			return _objectsByPass.ContainsKey(pass);
		}

		private IEnumerable<List<object>> ObjectsByPassValues()
		{
			return _objectsByPass.Values;
		}

		private IEnumerable<EdgeObject> GetOtherObjects()
		{
			return _otherObjects.Values;
		}

		private static void BuildExtraFields4Sql(EdgeObject obj, ref string columns, ref string values)
		{
			if (obj.ExtraFields == null) return;

			foreach (var field in obj.ExtraFields)
			{
				if (field.Key.ColumnType == "obj")
				{
					var edgeObj = field.Value as EdgeObject;
					if (edgeObj != null)
					{
						columns = String.Format("{0},\n{1}_Field{2}_GK", columns, field.Key.ColumnType, field.Key.ColumnIndex);
						values = String.Format("{0},\n{1}", values, edgeObj.GK);

						columns = String.Format("{0},\n{1}_Field{2}_TK", columns, field.Key.ColumnType, field.Key.ColumnIndex);
						values = String.Format("{0},\n'{1}'", values, edgeObj.TK.RemoveInvalidCharacters());

						columns = String.Format("{0},\n{1}_Field{2}_type", columns, field.Key.ColumnType, field.Key.ColumnIndex);
						values = String.Format("{0},\n'{1}'", values, field.Key.FieldEdgeType.TypeID);
					}
				}
				else if (field.Key.ColumnType == "string")
				{
					columns = String.Format("{0},\n{1}_Field{2}", columns, field.Key.ColumnType, field.Key.ColumnIndex);
					values = String.Format("{0},\n'{1}'", values, field.Value != null ? field.Value.ToString().RemoveInvalidCharacters() : field.Value);
				}
				else // INT and FLOAT
				{
					columns = String.Format("{0},\n{1}_Field{2}", columns, field.Key.ColumnType, field.Key.ColumnIndex);
					values = String.Format("{0},\n{1}", values, field.Value);
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
		private void BuildEdgeObjectFields(EdgeObject edgeObject, ref string columns, ref string values)
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
					columns = String.Format("{0},\n{1}_Field{2}", columns, field.ColumnType, field.ColumnIndex);

					var val = fieldInfo.GetValue(edgeObject);
					// prepare insert according to the field type
					if (fieldInfo.FieldType == typeof (string))
					{
						values = String.Format("{0},\n'{1}'", values, val != null ? val.ToString().RemoveInvalidCharacters() : val);
					}
					else if (fieldInfo.FieldType.BaseType == typeof (Enum))
					{
						// parsing to enum
						values = String.Format("{0},\n{1}", values, (int) Enum.Parse(fieldInfo.FieldType, val.ToString()));
					}
					else
					{
						// default for int, float and others
						values = String.Format("{0},\n{1}", values, val);
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
		private void BuildSpecificFields(EdgeObject edgeObject, ref string columns, ref string values)
		{
			if (edgeObject is Ad)
			{
				var ad = edgeObject as Ad;
				// destination Url
				columns = String.Format("{0},\nDestinationUrl", columns);
				values = String.Format("{0},\n'{1}'", values, ad.DestinationUrl);

				// creative
				columns = String.Format("{0},\nCreativeGK", columns);
				values = String.Format("{0},\n{1}", values, ad.CreativeDefinition.Creative.GK);

				columns = String.Format("{0},\nCreativeTK", columns);
				values = String.Format("{0},\n'{1}'", values, ad.CreativeDefinition.Creative.TK);

				columns = String.Format("{0},\nCreativeTypeID", columns);
				values = String.Format("{0},\n{1}", values, EdgeTypes.Values.Where(x => x.ClrType == ad.CreativeDefinition.Creative.GetType()).Select(x => x.TypeID).FirstOrDefault());
			}
			else 
			{
				// Type ID is defined in all objects except Ad
				columns = String.Format("{0},\nTypeID", columns);
				values = String.Format("{0},\n{1}", values, edgeObject.EdgeType.TypeID);
			}

			// fields defined for channel specific objects
			if (edgeObject is ChannelSpecificObject)
			{
				var channelObj = edgeObject as ChannelSpecificObject;
				columns = String.Format("{0},\nChannelID", columns);
				values = String.Format("{0},\n{1}", values, channelObj.Channel.ID);

				if (!String.IsNullOrEmpty(channelObj.OriginalID))
				{
					columns = String.Format("{0},\nOriginalID", columns);
					values = String.Format("{0},\n'{1}'", values, channelObj.OriginalID);
				}

				columns = String.Format("{0},\nStatus", columns);
				values = String.Format("{0},\n{1}", values, (int)channelObj.Status);
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
				if (obj is KeyValuePair<ExtraField, object> && ((KeyValuePair<ExtraField, object>) obj).Value is EdgeObject)
				{
					edgeObj = ((KeyValuePair<ExtraField, object>)obj).Value as EdgeObject;
				}
				else if (obj is KeyValuePair<CompositePartField, EdgeObject>)
				{
					edgeObj = ((KeyValuePair<CompositePartField, EdgeObject>)obj).Value;
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
							if ((obj is KeyValuePair<ExtraField, object>))
							{
								var extraField = (KeyValuePair<ExtraField, object>)obj;
								if (EdgeTypes.ContainsKey(extraField.Key.Name))
								{
									edgeObj.EdgeType = EdgeTypes[extraField.Key.Name];
								}
								else
								{
									throw new ConfigurationErrorsException(String.Format("Edge type is not set for extra field {0} and cannot be found in EdgeTypes", extraField.Key.Name));
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
			_objectsByPass.Clear();
			_allObjects.Clear();
			_otherObjects.Clear();
		}

		#endregion
	}
}
