using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using Edge.Core.Configuration;
using Edge.Core.Utilities;
using Edge.Data.Objects;
using Edge.Data.Pipeline.Metrics.Misc;
using Edge.Data.Pipeline.Metrics.Services;
using Edge.Data.Pipeline.Objects;

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
				var values = String.Format("'{0}'", obj.Key);

				columns = String.Format("{0},\nGK", columns);
				values = String.Format("{0},\n{1}", values, obj.Value.GK);

				if (!(obj.Value is Ad))
				{
					// Type ID
					columns = String.Format("{0},\nTypeID", columns);
					values = String.Format("{0},\n{1}", values, obj.Value.EdgeType.TypeID);
				}
				// account
				columns = String.Format("{0},\nAccountID", columns);
				values = String.Format("{0},\n{1}", values, obj.Value.Account.ID);

				if (obj.Value is ChannelSpecificObject)
				{
					var channelObj = obj.Value as ChannelSpecificObject;
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

				// specific fields by object type
				BuildSpecificFields(obj.Value, ref columns, ref values);

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
			if (!_objectsCache.ContainsKey(obj.TK))
			{
				_objectsCache.Add(obj.TK, obj);
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

			return flatList;
		}

		private void ClearObjects()
		{
			_objectsByPass.Clear();
			_allObjects.Clear();
			_otherObjects.Clear();
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
				columns = String.Format("{0},\n{1}_Field{2}", columns, field.Key.ColumnType, field.Key.ColumnIndex);
				// TODO - how to support all types???
				values = String.Format("{0},\n'{1}'", values,
										(field.Value is StringValue) ? (field.Value as StringValue).Value : String.Empty);
			}
		}

		private void BuildSpecificFields(EdgeObject edgeObject, ref string columns, ref string values)
		{
			if (edgeObject is Ad)
			{
				columns = String.Format("{0},\nDestinationUrl", columns);
				values = String.Format("{0},\n'{1}'", values, (edgeObject as Ad).DestinationUrl);
			}
		}
		#endregion
	}
}
