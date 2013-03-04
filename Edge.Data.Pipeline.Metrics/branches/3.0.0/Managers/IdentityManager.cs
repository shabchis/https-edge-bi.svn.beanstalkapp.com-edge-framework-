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

namespace Edge.Data.Pipeline.Metrics.Managers
{
	/// <summary>
	/// Identity manager - set delivery objects GK according to identity fields
	/// </summary>
	internal class IdentityManager
	{
		#region Properties
		private readonly SqlConnection _deliverySqlConnection;
		private SqlConnection _objectsSqlConnection;

		public List<EdgeFieldDependencyInfo> Dependencies { get; set; }
		public string TablePrefix { get; set; }
		public int AccountId { get; set; }

		private const string SP_INSERT_NEW_EDGE_OBJECTS = "InsertNewEdgeObjects";
		private const string SP_UPDATE_EDGE_OBJECTS = "UpdateEdgeObjects";
		#endregion

		#region Ctor

		public IdentityManager(SqlConnection deliveryConnection)
		{
			_deliverySqlConnection = deliveryConnection;
		}
		#endregion

		#region Public Methods

		/// <summary>
		/// Identity stage I: update delivery objects with existing in EdgeObject DB object GKs 
		/// tag Delivery objects by IdentityStatus (New, Modified or Unchanged)
		/// </summary>
		public void IdentifyDeliveryObjects()
		{
			// load object dependencies
			Dependencies = EdgeObjectConfigLoader.GetEdgeObjectDependencies(AccountId).Values.ToList();

			int maxDependecyDepth = Dependencies.Max(x => x.Depth);
			for (int i = 0; i <= maxDependecyDepth; i++)
			{
				var currentDepth = i;
				foreach (var field in Dependencies.Where(x => x.Depth == currentDepth))
				{
					var deliveryObjects = GetDeliveryObjects(field.Field.FieldEdgeType);
					if (deliveryObjects == null) continue;

					using (var selectEdgeObjectCommand = PrepareSelectEdgeObjectCommand(field.Field.FieldEdgeType))
					using (var updateGkCommand = PrepareUpdateGkCommand(field))
					{
						foreach (var deliveryObject in deliveryObjects)
						{
							SetDeliveryObjectByEdgeObject(deliveryObject, selectEdgeObjectCommand);

							// TODO: add log GK was found or not found
							if (!String.IsNullOrEmpty((deliveryObject.GK)))
							{
								// update delivery with GK if GK was found by identity fields and set IdentityStatus accordingly (Modified or Unchanged)
								updateGkCommand.Parameters["@gk"].Value		= deliveryObject.GK;
								updateGkCommand.Parameters["@tk"].Value		= deliveryObject.TK;
								updateGkCommand.Parameters["@identityStatus"].Value = deliveryObject.IdentityStatus;
								updateGkCommand.ExecuteNonQuery();
							}
						}
					}
					// drop temp table of EdgeObject by edge type
				}
			}
		}
		
		/// <summary>
		/// Identity stage II: update EdgeObject DB (staging) with edge object from Delivery DB:
		/// * insert new EdgeObjects --> IdentityStatus = New
		/// * update modifued EdgeObjects --> IdentityStatus = Modified
		/// </summary>
		public void UpdateEdgeObjects(SqlTransaction transaction)
		{
			// insert new EdgeObjects from Delivery into EdgeObjects DB
			using (var insertCmd = SqlUtility.CreateCommand(SP_INSERT_NEW_EDGE_OBJECTS, CommandType.StoredProcedure))
			{
				insertCmd.Parameters.AddWithValue("@TablePrefix", string.Format("{0}_", TablePrefix));
				insertCmd.Connection = _deliverySqlConnection;
				insertCmd.Transaction = transaction;
				insertCmd.ExecuteNonQuery();
			}

			// update exsting EdgeObject with new data (only Modified)
			using (var updateCmd = SqlUtility.CreateCommand(SP_UPDATE_EDGE_OBJECTS, CommandType.StoredProcedure))
			{
				updateCmd.Parameters.AddWithValue("@TablePrefix", string.Format("{0}_", TablePrefix));
				updateCmd.Connection = _deliverySqlConnection;
				updateCmd.Transaction = transaction;
				updateCmd.ExecuteNonQuery();
			}
		}

		#endregion

		#region Private Methods

		/// <summary>
		/// Retrieve EdgeObject according to delivery object ideintity fields to get GK
		/// if found, check if additional fields were changed and set Status to accordingly
		/// </summary>
		/// <param name="deliveryObject"></param>
		/// <param name="selectEdgeObjectCommand"></param>
		private void SetDeliveryObjectByEdgeObject(DeliveryEdgeObject deliveryObject, SqlCommand selectEdgeObjectCommand)
		{
			// set identity fields parameters values to retrieve relevant edge object
			foreach (var identity in deliveryObject.FieldList.Where(x => x.IsIdentity))
			{
				selectEdgeObjectCommand.Parameters[String.Format("@{0}", identity.FieldName)].Value = identity.Value;
			}

			using (var reader = selectEdgeObjectCommand.ExecuteReader())
			{
				while (reader.Read())
				{
					// if found set GK
					deliveryObject.GK = reader["GK"].ToString();
					deliveryObject.IdentityStatus = DeliveryObjectStatus.Unchanged;

					// check if additional fields where changed, if yes --> set status to Modified
					foreach (var field in deliveryObject.FieldList.Where(x => !x.IsIdentity))
					{
						if (field.Value != reader[field.FieldName].ToString())
						{
							deliveryObject.IdentityStatus = DeliveryObjectStatus.Modified;
							break;
						}
					}
				}
			}
		}

		/// <summary>
		/// Create temporary table by EdgeType, index it by identity fields and insert all EdgeObjects into it -
		/// all this for provide fast retrivals and avoid EdgeObject tbale locks
		/// Prepare SQL command for retrieving object GK from TEMP EdgeObject table by identity fields
		/// </summary>
		/// <param name="edgeType"></param>
		/// <returns></returns>
		private SqlCommand PrepareSelectEdgeObjectCommand(EdgeType edgeType)
		{
			var selectColumnStr = "GK,";
			var whereParamsStr = String.Empty;
			var indexesStr = String.Empty;
			var tempTableName = String.Format("{0}_TEMP", edgeType.Name);
			
			var selectCmd = new SqlCommand { Connection = ObjectsDbConnection() };
			var createTempTableCmd = new SqlCommand { Connection = ObjectsDbConnection() };
			createTempTableCmd.Parameters.Add(new SqlParameter("@typeId", edgeType.TypeID));

			foreach (var field in edgeType.Fields)
			{
				// add columns to SELECT (later check if edge object was updated or not)
				selectColumnStr = String.Format("{0}{1},", selectColumnStr, field.IdentityColumnName);
				if (field.IsIdentity)
				{
					// add identity fields to WHERE
					whereParamsStr = String.Format("{0}{1}=@{1} AND ", whereParamsStr, field.IdentityColumnName);
					selectCmd.Parameters.Add(new SqlParameter(String.Format("@{0}", field.IdentityColumnName), null));

					// TODO: create index on each identity field
					indexesStr = String.Format("{0}CREATE INDEX on field {1};\n", indexesStr, field.IdentityColumnName);
				}
			}

			selectColumnStr = selectColumnStr.Remove(selectColumnStr.Length - 1, 1);
			if (whereParamsStr.Length > 5) whereParamsStr = whereParamsStr.Remove(whereParamsStr.Length - 5, 5);

			// TODO: create temp table of EdgeObjects
			createTempTableCmd.CommandText = String.Format("CREATE TABLE {0} INSERT .... SELECT {1} WHERE typeId=@typeId; {2}", 
											 tempTableName, selectColumnStr, indexesStr);
			createTempTableCmd.ExecuteNonQuery();

			selectCmd.CommandText = String.Format("SELECT {0} FROM {1} WHERE {2}", selectColumnStr, tempTableName, whereParamsStr);
			return selectCmd;
		}

		/// <summary>
		/// Prepare update GK command by TK which contains updates GK in all tables which contains this object:
		/// 1. Object table itself
		/// 2. All dependent parent tables
		/// 3. Metrics table
		/// </summary>
		/// <param name="field"></param>
		/// <returns></returns>
		private SqlCommand PrepareUpdateGkCommand(EdgeFieldDependencyInfo field)
		{
			var sqlCmd = new SqlCommand { Connection = _deliverySqlConnection };
			sqlCmd.Parameters.Add(new SqlParameter("@gk", null));
			sqlCmd.Parameters.Add(new SqlParameter("@tk", null));
			sqlCmd.Parameters.Add(new SqlParameter("@typeId", field.Field.FieldEdgeType.TypeID));
			sqlCmd.Parameters.Add(new SqlParameter("@identityStatus", null));

			var sqlList = new List<string>();
			// add edge type table update
			sqlList.Add(String.Format("UPDATE {0} SET GK=@gk, IdentityStatus=@identityStatus WHERE TK=@tk AND TYPEID=@typeId;\n", GetDeliveryTableName(field.Field.FieldEdgeType.TableName)));

			// create udate SQL for each dependent object 
			foreach (var dependent in field.DependentFields)
			{
				sqlList.Add(String.Format("UPDATE {0} SET {1}_GK=@gk WHERE {1}_TK=@tk AND {1}_TYPE=@typeId AND TYPEID=@{2}_typeId;\n",
											GetDeliveryTableName(dependent.Key.FieldEdgeType.TableName),
											dependent.Value.ColumnName, dependent.Value.Field.Name));

				sqlCmd.Parameters.Add(new SqlParameter(String.Format("@{0}_typeId", dependent.Value.Field.Name), dependent.Value.Field.FieldEdgeType.TypeID));
			}

			// update Metrics SQL
			sqlList.Add(String.Format("UPDATE {0} SET {1}_GK=@gk WHERE {1}_TK=@tk;\n", GetDeliveryTableName("Metrics"), field.Field.Name));

			// combine all SQLs into one sql command
			sqlCmd.CommandText = String.Join(String.Empty, sqlList);

			return sqlCmd;
		}

		private SqlConnection ObjectsDbConnection()
		{
			if (_objectsSqlConnection == null)
			{
				var connectionString = AppSettings.GetConnectionString(typeof(MetricsProcessorServiceBase), Consts.ConnectionStrings.Objects);
				_objectsSqlConnection = new SqlConnection(connectionString);
				_objectsSqlConnection.Open();
			}
			return _objectsSqlConnection;
		}

		/// <summary>
		/// Load objects by type from Delivery DB (by identity fields)
		/// </summary>
		/// <param name="edgeType"></param>
		/// <returns></returns>
		private IEnumerable<DeliveryEdgeObject> GetDeliveryObjects(EdgeType edgeType)
		{
			var columnsStr = String.Empty;
			foreach (var field in edgeType.Fields)
			{
				columnsStr = String.Format("{0}{1},", columnsStr, field.IdentityColumnName);
			}
			if (columnsStr.Length == 0) return null;

			columnsStr = columnsStr.Remove(columnsStr.Length - 1, 1);

			var deliveryObjects = new List<DeliveryEdgeObject>();
			using (var command = new SqlCommand {Connection = _deliverySqlConnection})
			{ 
				command.CommandText = String.Format("SELECT TK, {0} FROM {1} WHERE TYPEID = @typeId",
														columnsStr,
														GetDeliveryTableName(edgeType.TableName));

				command.Parameters.Add(new SqlParameter("@typeId", edgeType.TypeID));
			
				using (var reader = command.ExecuteReader())
				{
					while (reader.Read())
					{
						var deliveryObj = new DeliveryEdgeObject { TK = reader["TK"].ToString() };
						foreach (var field in edgeType.Fields)
						{
							deliveryObj.FieldList.Add(new FieldValue
							{
								FieldName = field.IdentityColumnName,
								Value = reader[field.IdentityColumnName].ToString(),
								IsIdentity = field.IsIdentity
							});

						}
						deliveryObjects.Add(deliveryObj);
					}
				}
			}
			return deliveryObjects;
		}

		private string GetDeliveryTableName(string tableName)
		{
			return String.Format("[dbo].[{0}_{1}]", TablePrefix, tableName);
		}

		#endregion
	}
}
