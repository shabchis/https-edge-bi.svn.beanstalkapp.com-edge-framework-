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

			// TODO: create index on TK fields in Delivery objects tables

			int maxDependecyDepth = Dependencies.Max(x => x.Depth);
			for (int i = 0; i <= maxDependecyDepth; i++)
			{
				var currentDepth = i;
				foreach (var field in Dependencies.Where(x => x.Depth == currentDepth))
				{
					UpdateObjectDependencies(field.Field);

					var deliveryObjects = GetDeliveryObjects(field.Field.FieldEdgeType);
					if (deliveryObjects == null) continue;

					using (var selectEdgeObjectCommand = PrepareSelectEdgeObjectCommand(field.Field.FieldEdgeType))
					using (var updateGkCommand = PrepareUpdateGkCommand(field))
					{
						foreach (var deliveryObject in deliveryObjects)
						{
							SetDeliveryObjectByEdgeObject(deliveryObject, selectEdgeObjectCommand);

							// TODO: add log GK was found or not found
							// update delivery with GKs if GK was found by identity fields and set IdentityStatus accordingly (Modified or Unchanged)
							if (!String.IsNullOrEmpty(deliveryObject.GK))
							{
								updateGkCommand.Parameters["@gk"].Value = deliveryObject.GK;
								updateGkCommand.Parameters["@tk"].Value = deliveryObject.TK;
								updateGkCommand.Parameters["@identityStatus"].Value = deliveryObject.IdentityStatus;

								updateGkCommand.ExecuteNonQuery();
							}
						}
						CreateTempGkTkTable4Field(field.Field);
					}
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
		/// Create temporary table which contains GK, TK mapping of updated object type in Delivery
		/// </summary>
		/// <param name="field"></param>
		private void CreateTempGkTkTable4Field(EdgeField field)
		{
			using (var cmd = new SqlCommand { Connection = _deliverySqlConnection })
			{
				cmd.CommandText = String.Format("SELECT GK, TK INTO ##TempDelivery_{0} FROM {1} WHERE TYPEID=@typeId ",
												field.Name, GetDeliveryTableName(field.FieldEdgeType.TableName));
				cmd.Parameters.AddWithValue("@typeId", field.FieldEdgeType.TypeID);
				cmd.ExecuteNonQuery();
			}
		}

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
		/// <returns></returns>
		private SqlCommand PrepareSelectEdgeObjectCommand(EdgeType edgeType)
		{
			var selectColumnStr = "GK,";
			var whereParamsStr = String.Empty;
			var indexFieldsStr = String.Empty;
			var sideObjectTableName = GetDeliveryTableName(String.Format("_SIDE_{0}", edgeType.Name));
			
			var selectCmd = new SqlCommand { Connection = _deliverySqlConnection };
			var createTempTableCmd = new SqlCommand { Connection = _deliverySqlConnection };
			createTempTableCmd.Parameters.Add(new SqlParameter("@typeId", edgeType.TypeID));

			foreach (var field in edgeType.Fields)
			{
				// add columns to SELECT (later check if edge object was updated or not)
				selectColumnStr = String.Format("{0}{1},", selectColumnStr, field.ColumnNameGK);
				if (field.IsIdentity)
				{
					// add identity fields to WHERE
					whereParamsStr = String.Format("{0}{1}=@{1} AND ", whereParamsStr, field.ColumnNameGK);
					selectCmd.Parameters.Add(new SqlParameter(String.Format("@{0}", field.ColumnNameGK), null));

					indexFieldsStr = String.Format("{0}{1},", indexFieldsStr, field.ColumnNameGK);
				}
			}

			selectColumnStr = selectColumnStr.Remove(selectColumnStr.Length - 1, 1);
			indexFieldsStr = indexFieldsStr.Remove(indexFieldsStr.Length - 1, 1);
			if (whereParamsStr.Length > 5) whereParamsStr = whereParamsStr.Remove(whereParamsStr.Length - 5, 5);

			// create temp table from EdgeObjectd DB table by edge type + indexes on Identity fields
			createTempTableCmd.CommandText = String.Format(@"IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES where TABLE_NAME = '{5}')
										BEGIN
											SELECT {0} INTO {1} FROM [EdgeObjects].[dbo].{2} WHERE typeId=@typeId; 
											CREATE NONCLUSTERED INDEX [IDX_{4}] ON {1} ({3});
										END", 
											selectColumnStr,
											sideObjectTableName,
											edgeType.TableName, 
											indexFieldsStr,
											edgeType.Name,
											String.Format("{0}__SIDE_{1}", TablePrefix, edgeType.Name));
			
			createTempTableCmd.ExecuteNonQuery();
			createTempTableCmd.Dispose();

			selectCmd.CommandText = String.Format("SELECT {0} FROM {1} WHERE {2}", selectColumnStr, sideObjectTableName, whereParamsStr);
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

			sqlCmd.CommandText = String.Format("UPDATE {0} \nSET GK=@gk, IdentityStatus=@identityStatus \nWHERE TK=@tk AND TYPEID=@typeId",
												GetDeliveryTableName(field.Field.FieldEdgeType.TableName));

			sqlCmd.Parameters.Add(new SqlParameter("@gk", null));
			sqlCmd.Parameters.Add(new SqlParameter("@tk", null));
			sqlCmd.Parameters.Add(new SqlParameter("@typeId", field.Field.FieldEdgeType.TypeID));
			sqlCmd.Parameters.Add(new SqlParameter("@identityStatus", null));

			return sqlCmd;

			//var childsUpdateStr = String.Empty;
			//foreach (var childField in field.Field.FieldEdgeType.Fields.Where(x => x.Field.FieldEdgeType != null))
			//{
			//	childsUpdateStr = String.Format(",{0}{1}=@{1}", childsUpdateStr, childField.ColumnNameGK);
			//	sqlCmd.Parameters.Add(new SqlParameter(String.Format("@{0}", childField.ColumnNameGK), null));
			//}

			//var sqlList = new List<string>();
			//// add edge type table update
			//sqlList.Add(String.Format("UPDATE {0} SET GK=@gk, IdentityStatus=@identityStatus WHERE TK=@tk AND TYPEID=@typeId;", 
			//							GetDeliveryTableName(field.Field.FieldEdgeType.TableName)));

			
			// create udate SQL for each dependent object 
			//foreach (var dependent in field.DependentFields)
			//{
			//	sqlList.Add(String.Format("UPDATE {0} SET {1}_GK=@gk WHERE {1}_TK=@tk AND {1}_TYPE=@typeId AND TYPEID=@{2}_typeId;\n",
			//								GetDeliveryTableName(dependent.Key.FieldEdgeType.TableName),
			//								dependent.Value.ColumnName, dependent.Value.Field.Name));

			//	sqlCmd.Parameters.Add(new SqlParameter(String.Format("@{0}_typeId", dependent.Value.Field.Name), dependent.Value.Field.FieldEdgeType.TypeID));
			//}

			// update Metrics SQL
			//sqlList.Add(String.Format("UPDATE {0} SET {1}_GK=@gk WHERE {1}_TK=@tk;\n", GetDeliveryTableName("Metrics"), field.Field.Name));

			// combine all SQLs into one sql command
			//sqlCmd.CommandText = String.Join(String.Empty, sqlList);

			//return sqlCmd;
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
			var columnsStr = edgeType.Fields.Aggregate(String.Empty, (current, field) => String.Format("{0}{1},", current, field.ColumnNameGK));
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
									FieldName = field.ColumnNameGK,
									IsIdentity = field.IsIdentity,
									Value = reader[field.ColumnNameGK].ToString()
								});
						}
						deliveryObjects.Add(deliveryObj);
					}
				}
			}
			return deliveryObjects;
		}

		/// <summary>
		/// Before searching for object GKs update all GK of its parent fields 
		/// (objects it depends on)
		/// For example: before searching for AdGroup GKs, update all AdGroup Campaings
		/// </summary>
		/// <param name="field"></param>
		private void UpdateObjectDependencies(EdgeField field)
		{
			// nothitng to do if there are no GK to update
			if (field.FieldEdgeType.Fields.All(x => x.Field.FieldEdgeType == null)) return;

			var mainTableName = GetDeliveryTableName(field.FieldEdgeType.TableName);
			var setStr = String.Format("UPDATE {0} \nSET ", mainTableName);
			var fromStr = "FROM ";
			var whereStr = String.Format("WHERE {0}.TYPEID=@typeId AND ", mainTableName);
			var paramList = new List<SqlParameter> {new SqlParameter("@typeId", field.FieldEdgeType.TypeID)};

			foreach (var parentField in field.FieldEdgeType.Fields.Where(x => x.Field.FieldEdgeType != null))
			{
				var tempParentTableName = String.Format("##TempDelivery_{0}", parentField.Field.Name);

				fromStr = String.Format("{0} {1},", fromStr, tempParentTableName);
				setStr = String.Format("{0} {1}={2}.GK,", 
										setStr,
 										parentField.ColumnNameGK,
										tempParentTableName);
				whereStr = String.Format("{0}{1}.{2}={3}.TK AND ",
										 whereStr,
										 mainTableName,
										 parentField.ColumnNameTK,
										 tempParentTableName);
			}
			if (fromStr.Length > 0)  fromStr = fromStr.Remove(fromStr.Length - 1, 1); 
			if (setStr.Length > 0)   setStr = setStr.Remove(setStr.Length - 1, 1);
			if (whereStr.Length > 4) whereStr = whereStr.Remove(whereStr.Length - 5, 5);

			// perform update
			using (var cmd = new SqlCommand {Connection = _deliverySqlConnection})
			{
				cmd.CommandText = String.Format("{0}\n{1}\n{2}", setStr, fromStr, whereStr);
				cmd.Parameters.AddRange(paramList.ToArray());

				cmd.ExecuteNonQuery();
			}
		}

		private string GetDeliveryTableName(string tableName)
		{
			return String.Format("[dbo].[{0}_{1}]", TablePrefix, tableName);
		}

		#endregion
	}
}
