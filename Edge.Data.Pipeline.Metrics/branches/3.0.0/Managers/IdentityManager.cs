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
		public DateTime TransformTimestamp { get; set; }

		private const string SP_STAGE_METRICS = "StageMetrics";
		#endregion

		#region Ctor

		public IdentityManager(SqlConnection deliveryConnection)
		{
			_deliverySqlConnection = deliveryConnection;
		}
		#endregion

		#region Identity I

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
					deliveryObject.IdentityStatus = IdentityStatus.Unchanged;

					// check if additional fields where changed, if yes --> set status to Modified
					foreach (var field in deliveryObject.FieldList.Where(x => !x.IsIdentity))
					{
						if (field.Value != reader[field.FieldName].ToString())
						{
							deliveryObject.IdentityStatus = IdentityStatus.Modified;
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
			using (var command = new SqlCommand { Connection = _deliverySqlConnection })
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
			var paramList = new List<SqlParameter> { new SqlParameter("@typeId", field.FieldEdgeType.TypeID) };

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
			if (fromStr.Length > 0) fromStr = fromStr.Remove(fromStr.Length - 1, 1);
			if (setStr.Length > 0) setStr = setStr.Remove(setStr.Length - 1, 1);
			if (whereStr.Length > 4) whereStr = whereStr.Remove(whereStr.Length - 5, 5);

			// perform update
			using (var cmd = new SqlCommand { Connection = _deliverySqlConnection })
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

		#region Identity II
		/// <summary>
		/// Identity stage II: update EdgeObject DB (staging) with edge object from Delivery DB
		/// using LOCK on EdgeObject table:
		/// * sync last changes according transform timestamp
		/// * update modified EdgeObjects --> IdentityStatus = Modified
		/// * insert new EdgeObjects --> IdentityStatus = New
		/// * find best match staging table
		/// * insert delivery metrics into staging metrics table
		/// </summary>
		public void UpdateEdgeObjects(SqlTransaction transaction)
		{
			// load object dependencies
			Dependencies = EdgeObjectConfigLoader.GetEdgeObjectDependencies(AccountId).Values.ToList();

			int maxDependecyDepth = Dependencies.Max(x => x.Depth);
			for (int i = 0; i <= maxDependecyDepth; i++)
			{
				var currentDepth = i;
				foreach (var field in Dependencies.Where(x => x.Depth == currentDepth))
				{
					UpdateObjectDependencies(field.Field);

					SyncLastChangesWithLock(field.Field.FieldEdgeType);

					UpdateExistingEdgeObjectsByDelivery(field.Field.FieldEdgeType);

					InsertNewEdgeObjects(field.Field.FieldEdgeType);
				}
			}

			// call SP for find best match table and insert delivery metrics into staging
			using (var metricsCmd = SqlUtility.CreateCommand(SP_STAGE_METRICS, CommandType.StoredProcedure))
			{
				metricsCmd.Parameters.AddWithValue("@TablePrefix", string.Format("{0}_", TablePrefix));
				metricsCmd.Connection = _deliverySqlConnection;
				metricsCmd.Transaction = transaction;
				metricsCmd.ExecuteNonQuery();
			}
		}

		private void InsertNewEdgeObjects(EdgeType edgeType)
		{
			var fieldsStr = String.Empty;
			foreach (var field in edgeType.Fields)
			{
				fieldsStr = String.Format("{0}{1},{2}", fieldsStr, field.ColumnNameGK, 
										  field.Field.FieldEdgeType != null ? 
										  String.Format("{0}_tk,", field.ColumnName) : String.Empty);
			}

			if (fieldsStr.Length == 0) return;

			fieldsStr = fieldsStr.Remove(fieldsStr.Length - 1, 1);
			// in one command insert new EdgeObjects and update delivery objects with inserted GKs by TK- hope will work
			using (var cmd = new SqlCommand {Connection = ObjectsDbConnection()})
			{
				cmd.CommandText = String.Format(@"CREATE TABLE #TEMP (GL BIGINT, TK NVARCHAR(50) 
												  INSERT INTO {0} ({1})
												  OUTPUT INSERTED.PID, [EdgeDeliveries].{2}.TK INTO #TEMP
												  SELECT {1} FROM [EdgeDeliveries].{2} 
												  WHERE TYPEID=@typeId AND IDENTITYSTATUS=0;
												UPDATE {2} SET GK=#TEMP.GK, IDENTITYSTATUS=1 FROM #TEMP WHERE TK=#TEMP.TK AND TYPEID=@typeId",
											edgeType.TableName, 
											fieldsStr,
											GetDeliveryTableName(edgeType.TableName));
				cmd.Parameters.AddWithValue("@typeId", edgeType.TypeID);
				cmd.ExecuteNonQuery();
			}
		}

		/// <summary>
		/// Update existing EdgeObject by modified objects from Delivery
		/// </summary>
		/// <param name="edgeType"></param>
		private void UpdateExistingEdgeObjectsByDelivery(EdgeType edgeType)
		{
			var updateFieldStr = String.Empty;
			foreach (var field in edgeType.Fields.Where(x => !x.IsIdentity))
			{
				updateFieldStr = String.Format("{0}{1}=deliveryTable.{1},", updateFieldStr, field.ColumnNameGK);
			}
			if (updateFieldStr.Length == 0) return;

			updateFieldStr = updateFieldStr.Remove(updateFieldStr.Length - 1, 1);
			using (var cmd = new SqlCommand { Connection = ObjectsDbConnection() })
			{
				cmd.CommandText = String.Format("UPDATE {0} \nSET {1} \nFROM {2} deliveryTable \nWHERE GK=deliveryTable.GK AND deliveryTable.IdentityStatus=2",
												edgeType.TableName,
												updateFieldStr,
												GetDeliveryTableName(edgeType.TableName));
				cmd.ExecuteNonQuery();
			}
		}

		/// <summary>
		/// Update delivery with the last changes in EdgeObjects by Transform timestamp 
		/// with Lock in order to avoid meanwhile EdgeObjects updates
		/// </summary>
		/// <param name="edgeType"></param>
		private void SyncLastChangesWithLock(EdgeType edgeType)
		{
			var updatedObjects = GetLastUpdatedEdgeObjects(edgeType);
			using (var updateCmd = PrepareSyncDeliveryObjectsCommand(edgeType))
			{
				foreach (var obj in updatedObjects)
				{
					updateCmd.Parameters["@gk"].Value = obj.GK;
					updateCmd.Parameters["@identityStatus"].Value = (int)IdentityStatus.Unchanged;

					foreach (var identityField in obj.FieldList.Where(x => x.IsIdentity))
					{
						updateCmd.Parameters[String.Format("@{0}", identityField.FieldName)].Value = identityField.Value;
					}
				}
				updateCmd.ExecuteNonQuery();
			}
		}

		private SqlCommand PrepareSyncDeliveryObjectsCommand(EdgeType edgeType)
		{
			var cmd = new SqlCommand { Connection = _deliverySqlConnection };
			var whereStr = String.Format("WHERE TYPEID=@typeId AND ");

			foreach (var field in edgeType.Fields.Where(x => x.IsIdentity))
			{
				whereStr = String.Format("{0}{1}=@{1} AND ", whereStr, field.ColumnNameGK);
				cmd.Parameters.AddWithValue(String.Format("@{0}", field.ColumnNameGK), null);
			}
			whereStr = whereStr.Remove(whereStr.Length - 5, 5);

			cmd.CommandText = String.Format("UPDATE {0} \nSET GK=@gk, IDENTITYSTATUS=@identityStatus \nWHERE {1}",
											GetDeliveryTableName(edgeType.TableName),
											whereStr);
			return cmd;
		}

		private IEnumerable<DeliveryEdgeObject> GetLastUpdatedEdgeObjects(EdgeType edgeType)
		{
			var deltaObjectList = new List<DeliveryEdgeObject>();

			var selectColumnStr = edgeType.Fields.Aggregate("SELECT GK,", (current, field) => String.Format("{0}{1},", current, field.ColumnNameGK));
			selectColumnStr = selectColumnStr.Remove(selectColumnStr.Length - 1, 1);

			using (var cmd = new SqlCommand { Connection = ObjectsDbConnection() })
			{
				cmd.CommandText = String.Format("{0} FROM {1} WHERE TYPEID=@typeId AND LASTUPDATED > @timestamp", selectColumnStr, edgeType.TableName);
				cmd.Parameters.AddWithValue("@typeId", edgeType.TypeID);
				cmd.Parameters.AddWithValue("@timestamp", TransformTimestamp);

				using (var reader = cmd.ExecuteReader())
				{
					while (reader.Read())
					{
						var obj = new DeliveryEdgeObject { GK = reader["GK"].ToString() };
						foreach (var field in edgeType.Fields)
						{
							obj.FieldList.Add(new FieldValue
								{
									FieldName = field.ColumnNameGK,
									IsIdentity = field.IsIdentity,
									Value = reader[field.ColumnNameGK].ToString()
								});
						}
						deltaObjectList.Add(obj);
					}
				}
			}
			return deltaObjectList;
		} 
		#endregion
	}
}
