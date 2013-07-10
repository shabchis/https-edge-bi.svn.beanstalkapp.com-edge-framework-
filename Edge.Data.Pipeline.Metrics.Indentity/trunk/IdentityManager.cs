using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using Edge.Data.Objects;

namespace Edge.Data.Pipeline.Metrics.Indentity
{
	public enum LogMessageType
	{
		Verbose = 0,
		Information = 1,
		Warning = 5,
		Error = 7,
		Debug = 8
	};

	/// <summary>
	/// Identity manager - set delivery objects GK according to identity fields
	/// </summary>
	public class IdentityManager
	{
		#region Properties
		private readonly SqlConnection _objectsSqlConnection;
		private SqlCommand _logCommand;
		private Dictionary<string, EdgeType> _edgeTypes;

		public List<EdgeFieldDependencyInfo> Dependencies { get; set; }
		public string TablePrefix { get; set; }
		public int AccountId { get; set; }
		public DateTime TransformTimestamp { get; set; }
		public bool CreateNewEdgeObjects { get; set; }

		public Dictionary<string, EdgeType> EdgeTypes
		{
			get 
			{
				return _edgeTypes ?? (_edgeTypes = EdgeObjectConfigLoader.LoadEdgeTypes(AccountId, _objectsSqlConnection));
			}
		}
		#endregion

		#region Ctor

		public IdentityManager(SqlConnection objectsConnection)
		{
			_objectsSqlConnection = objectsConnection;
			CreateNewEdgeObjects  = true;

			PrepareLogCommand();
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
			Dependencies = EdgeObjectConfigLoader.GetEdgeObjectDependencies(AccountId, _objectsSqlConnection).Values.ToList();
			Log("IdentifyDeliveryObjects:: EdgeObjects dependencies loaded");

			int maxDependecyDepth = Dependencies.Max(x => x.Depth);
			for (int i = 0; i <= maxDependecyDepth; i++)
			{
				var currentDepth = i;
				Log(String.Format("IdentifyDeliveryObjects:: dependency depth={0}", currentDepth));

				foreach (var field in Dependencies.Where(x => x.Depth == currentDepth))
				{
					Log(String.Format("IdentifyDeliveryObjects:: starting identify field '{0}'", field.Field.Name));
					UpdateObjectDependencies(field.Field);

					var deliveryObjects = GetDeliveryObjects(field.Field.FieldEdgeType);
					if (deliveryObjects == null)
					{
						CreateTempGkTkTable4Field(field.Field.FieldEdgeType); // even if there are no objects from temp table
						Log(String.Format("IdentifyDeliveryObjects:: there are no objects for field '{0}' of type '{1}'", field.Field.Name, field.Field.FieldEdgeType.Name));
						continue;
					}

					using (var selectEdgeObjectCommand = PrepareSelectEdgeObjectCommand(field.Field.FieldEdgeType))
					using (var updateGkCommand = PrepareUpdateGkCommand(field))
					{
						foreach (var deliveryObject in deliveryObjects)
						{
							SetDeliveryObjectByEdgeObject(deliveryObject, selectEdgeObjectCommand);

							// update delivery with GKs if GK was found by identity fields and set IdentityStatus accordingly (Modified or Unchanged)
							if (!String.IsNullOrEmpty(deliveryObject.GK))
							{
								updateGkCommand.Parameters["@gk"].Value = deliveryObject.GK;
								updateGkCommand.Parameters["@tk"].Value = deliveryObject.TK;
								updateGkCommand.Parameters["@identityStatus"].Value = deliveryObject.IdentityStatus;

								updateGkCommand.ExecuteNonQuery();
								Log(String.Format("IdentifyDeliveryObjects:: GK={0} was updated for {2} with TK={1}, identity status={3}",
												  deliveryObject.GK, deliveryObject.TK, field.Field.Name, deliveryObject.IdentityStatus));
							}
							else
								Log(String.Format("IdentifyDeliveryObjects:: GK={0} was not found for {2} with TK={1}", deliveryObject.GK, deliveryObject.TK, field.Field.Name));
						}
						CreateTempGkTkTable4Field(field.Field.FieldEdgeType);
						Log(String.Format("IdentifyDeliveryObjects:: Temp GK-TK table was created for {0}", field.Field.Name));
					}
				}
			}
		}

		/// <summary>
		/// Create temporary table which contains GK, TK mapping of updated object type in Delivery
		/// </summary>
		private void CreateTempGkTkTable4Field(EdgeType edgeType)
		{
			// do not create table for abstract objects
			if (edgeType.IsAbstract) return;

			using (var cmd = new SqlCommand { Connection = _objectsSqlConnection })
			{
				cmd.CommandText = String.Format(@"IF NOT EXISTS (SELECT * FROM TEMPDB.INFORMATION_SCHEMA.TABLES where TABLE_NAME = '##TempDelivery_{0}')
													BEGIN
														SELECT GK, TK INTO ##TempDelivery_{0} FROM {1} WHERE TYPEID=@typeId;
														CREATE NONCLUSTERED INDEX [IDX_{0}_TK] ON ##TempDelivery_{0} (TK);
													END",
												edgeType.Name, GetDeliveryTableName(edgeType.TableName));
				cmd.Parameters.AddWithValue("@typeId", edgeType.TypeID);
				cmd.ExecuteNonQuery();
			}
		}

		/// <summary>
		/// Retrieve EdgeObject according to delivery object ideintity fields to get GK
		/// if found, check if additional fields were changed and set Status to accordingly
		/// </summary>
		private void SetDeliveryObjectByEdgeObject(DeliveryEdgeObject deliveryObject, SqlCommand selectEdgeObjectCommand)
		{
			// set identity fields parameters values to retrieve relevant edge object
			foreach (var identity in deliveryObject.FieldList.Where(x => x.IsIdentity))
			{
				selectEdgeObjectCommand.Parameters[String.Format("@{0}", identity.FieldName)].Value = !String.IsNullOrEmpty(identity.Value) ? (object)identity.Value : DBNull.Value;
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
			if (!edgeType.Fields.Any(x => x.IsIdentity))
				throw new Exception(String.Format("There are no identity fields defined for type {0}", edgeType.Name));
			
			var selectColumnStr = "GK,";
			var whereParamsStr = String.Empty;
			var indexFieldsStr = String.Empty;
			var tempObjectTableName = String.Format("##TempEdgeObject_{0}", edgeType.Name);

			var selectCmd = new SqlCommand { Connection = _objectsSqlConnection };
			var createTempTableCmd = new SqlCommand { Connection = _objectsSqlConnection };
			createTempTableCmd.Parameters.Add(new SqlParameter("@typeId", edgeType.TypeID));

			foreach (var field in edgeType.Fields)
			{
				if (selectColumnStr.Contains(String.Format("{0},", field.ColumnNameGK))) continue;
				
				// add columns to SELECT (later check if edge object was updated or not)
				selectColumnStr = String.Format("{0}{1},", selectColumnStr, field.ColumnNameGK);
				if (field.IsIdentity)
				{
					// add identity fields to WHERE (some fields could be null when using edge type inheritance)
					whereParamsStr = String.Format("{0}(@{1} IS NULL OR {1} IS NULL OR {1}= @{1}) AND ", whereParamsStr,
					                               field.ColumnNameGK);
					var param = new SqlParameter(String.Format("@{0}", field.ColumnNameGK), null) {IsNullable = true};
					selectCmd.Parameters.Add(param);

					indexFieldsStr = String.Format("{0}{1},", indexFieldsStr, field.ColumnNameGK);
				}
			}

			selectColumnStr = selectColumnStr.Remove(selectColumnStr.Length - 1, 1);
			indexFieldsStr = indexFieldsStr.Remove(indexFieldsStr.Length - 1, 1);
			if (whereParamsStr.Length > 5) whereParamsStr = whereParamsStr.Remove(whereParamsStr.Length - 5, 5);

			// create temp table from EdgeObjectd DB table by edge type + indexes on Identity fields
			createTempTableCmd.CommandText = String.Format(@"IF NOT EXISTS (SELECT * FROM TEMPDB.INFORMATION_SCHEMA.TABLES where TABLE_NAME = '{1}')
										BEGIN
											SELECT {0} INTO {1} FROM {2} WHERE typeId=@typeId; 
											CREATE NONCLUSTERED INDEX [IDX_{4}] ON {1} ({3});
										END",
											selectColumnStr,
											tempObjectTableName,
											edgeType.TableName,
											indexFieldsStr,
											edgeType.Name);

			createTempTableCmd.ExecuteNonQuery();
			createTempTableCmd.Dispose();

			selectCmd.CommandText = String.Format("SELECT {0} FROM {1} WHERE {2}", selectColumnStr, tempObjectTableName, whereParamsStr);
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
			var sqlCmd = new SqlCommand
			{
				Connection = _objectsSqlConnection,
				CommandText = String.Format("UPDATE {0} \nSET GK=@gk, IdentityStatus=@identityStatus \nWHERE TK=@tk AND TYPEID=@typeId",
											GetDeliveryTableName(field.Field.FieldEdgeType.TableName))
			};

			sqlCmd.Parameters.Add(new SqlParameter("@gk", null));
			sqlCmd.Parameters.Add(new SqlParameter("@tk", null));
			sqlCmd.Parameters.Add(new SqlParameter("@typeId", field.Field.FieldEdgeType.TypeID));
			sqlCmd.Parameters.Add(new SqlParameter("@identityStatus", null));

			return sqlCmd;
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
			using (var command = new SqlCommand { Connection = _objectsSqlConnection })
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
			var paramList = new List<SqlParameter> { new SqlParameter("@typeId", field.FieldEdgeType.TypeID) };
			var cmdStr = String.Empty;

			foreach (var parentField in field.FieldEdgeType.Fields.Where(x => x.Field.FieldEdgeType != null))
			{
				foreach (var childType in EdgeObjectConfigLoader.FindEdgeTypeInheritors(parentField.Field.FieldEdgeType, EdgeTypes))
				{
					var tempParentTableName = String.Format("##TempDelivery_{0}", childType.Name);
					cmdStr = String.Format("{0}UPDATE {1} SET {3}={2}.GK FROM {2} WHERE {1}.TYPEID=@typeId AND {1}.{4}={2}.TK;\n\n",
											cmdStr,		
											mainTableName,
											tempParentTableName,
											parentField.ColumnNameGK,
											parentField.ColumnNameTK);
				}
			}
			
			// perform update
			using (var cmd = new SqlCommand { Connection = _objectsSqlConnection })
			{
				cmd.CommandText = cmdStr;
				cmd.Parameters.AddRange(paramList.ToArray());

				cmd.ExecuteNonQuery();
			}
			Log(String.Format("UpdateObjectDependencies:: dependencies updated for field '{0}'", field.Name));
		}

		private string GetDeliveryTableName(string tableName)
		{
			if (String.IsNullOrEmpty(TablePrefix))
				throw new Exception("Table prefix is not set for IdenityManager");

			return String.Format("[EdgeDeliveries].[dbo].[{0}_{1}]", TablePrefix, tableName);
		}

		#endregion

		#region Identity II
		/// <summary>
		/// Identity stage II: update EdgeObject DB (staging) with edge object from Delivery DB
		/// using LOCK on EdgeObject table:
		/// * sync last changes according transform timestamp
		/// * update modified EdgeObjects --> IdentityStatus = Modified
		/// * insert new EdgeObjects --> IdentityStatus = New
		/// </summary>
		public void UpdateEdgeObjects()
		{
			// load object dependencies
			Dependencies = EdgeObjectConfigLoader.GetEdgeObjectDependencies(AccountId, _objectsSqlConnection).Values.ToList();
			Log("UpdateEdgeObjects:: EdgeObjects dependencies loaded");

			int maxDependecyDepth = Dependencies.Max(x => x.Depth);
			for (int i = 0; i <= maxDependecyDepth; i++)
			{
				var currentDepth = i;
				Log(String.Format("UpdateEdgeObjects:: dependency depth={0}", currentDepth));

				foreach (var field in Dependencies.Where(x => x.Depth == currentDepth))
				{
					Log(String.Format("UpdateEdgeObjects:: starting update field '{0}'", field.Field.Name));
					UpdateObjectDependencies(field.Field);

					if (DeliveryContainsChanges(field.Field.FieldEdgeType, IdentityStatus.Unchanged, true))
					{
						Log(String.Format("UpdateEdgeObjects:: delivery contains changes of {0}, starting sync", field.Field.Name));
						
						SyncLastChangesWithLock(field.Field.FieldEdgeType);

						if (DeliveryContainsChanges(field.Field.FieldEdgeType, IdentityStatus.Modified))
						{
							UpdateExistingEdgeObjectsByDelivery(field.Field.FieldEdgeType);
							Log(String.Format("UpdateEdgeObjects:: update modified object of type '{0}'", field.Field.FieldEdgeType));
						}

						if (CreateNewEdgeObjects && DeliveryContainsChanges(field.Field.FieldEdgeType, IdentityStatus.New))
						{
							InsertNewEdgeObjects(field.Field.FieldEdgeType);
							Log(String.Format("UpdateEdgeObjects:: insert new objects of type '{0}'", field.Field.FieldEdgeType));
						}
					}
					else
						Log(String.Format("UpdateEdgeObjects:: delivery doen't contain changes of {0}, nothing to sync", field.Field.Name));

					CreateTempGkTkTable4Field(field.Field.FieldEdgeType);
				}
			}
		}

		/// <summary>
		/// Check if delivery contains changes by EdgeType
		/// </summary>
		/// <param name="edgeType"></param>
		/// <param name="status"></param>
		/// <param name="isNot"></param>
		/// <returns></returns>
		private bool DeliveryContainsChanges(EdgeType edgeType, IdentityStatus status, bool isNot = false)
		{
			using (var cmd = new SqlCommand { Connection = _objectsSqlConnection })
			{
				cmd.CommandText = String.Format("SELECT Count(*) Count FROM {0} WHERE IdentityStatus {1} @identityStatus AND TYPEID=@typeId",
												GetDeliveryTableName(edgeType.TableName),
												isNot ? "<>" : "=");
				cmd.Parameters.AddWithValue("@identityStatus", (int)status);
				cmd.Parameters.AddWithValue("@typeId", edgeType.TypeID);

				using (var reader = cmd.ExecuteReader())
				{
					if (reader.Read()) return int.Parse(reader["Count"].ToString()) > 0;
				}
			}
			return false;
		}

		/// <summary>
		/// Insert new EdgeObjects, generate GK and update GK according to identity fields in Deliveru tables 
		/// for later UpdateDependencies
		/// </summary>
		/// <param name="edgeType"></param>
		private void InsertNewEdgeObjects(EdgeType edgeType)
		{
			if (edgeType.Fields.Count == 0) return;

			var createfieldsStr = "GK BIGINT,";
			var whereStr = "TYPEID=@typeId AND ";
			var outputStr = "INSERTED.GK,";
			var fieldsStr = String.Format("CREATEDON,LASTUPDATEDON,TYPEID,ACCOUNTID,ROOTACCOUNTID,{0}",
							edgeType.ClrType.IsSubclassOf(typeof(ChannelSpecificObject)) ? "CHANNELID," : String.Empty);

			foreach (var field in edgeType.Fields)
			{
				if (!fieldsStr.Contains(String.Format("{0},", field.ColumnNameGK)))
					fieldsStr = String.Format("{0}{1},", fieldsStr, field.ColumnNameGK);
				if (field.Field.FieldEdgeType != null && !fieldsStr.Contains(String.Format("{0}_type,", field.ColumnName)))
					fieldsStr = String.Format("{0}{1}_type,", fieldsStr, field.ColumnName);

				if (field.IsIdentity)
				{
					createfieldsStr = String.Format("{0}{1} {2},", createfieldsStr, field.ColumnNameGK, field.ColumnDbType); //EdgeObjectConfigLoader.GetDbFieldType(field));
					whereStr = String.Format("{0}({2}.{1}=#TEMP.{1} OR {2}.{1} IS NULL) AND ", whereStr, field.ColumnNameGK, GetDeliveryTableName(edgeType.TableName));
					outputStr = String.Format("{0}INSERTED.{1},", outputStr, field.ColumnNameGK);
				}
			}

			fieldsStr = fieldsStr.Remove(fieldsStr.Length - 1, 1);
			createfieldsStr = createfieldsStr.Remove(createfieldsStr.Length - 1, 1);
			outputStr = outputStr.Remove(outputStr.Length - 1, 1);
			whereStr = whereStr.Remove(whereStr.Length - 5, 5);

			// in one command insert new EdgeObjects and update delivery objects with inserted GKs by TK- hope will work
			using (var cmd = new SqlCommand { Connection = _objectsSqlConnection })
			{
				cmd.CommandText = String.Format(@"CREATE TABLE #TEMP ({0})
												  INSERT INTO {1} ({2})
												  OUTPUT {5} INTO #TEMP
												  SELECT {6} FROM {3} 
												  WHERE TYPEID=@typeId AND IDENTITYSTATUS=@newStatus;

												UPDATE {3} SET GK=#TEMP.GK, IDENTITYSTATUS=@unchangesStatus FROM #TEMP, {3} WHERE {4};

												DROP TABLE #TEMP;",

											createfieldsStr,
											edgeType.TableName,
											fieldsStr,
											GetDeliveryTableName(edgeType.TableName),
											whereStr,
											outputStr,
											fieldsStr.Replace("CREATEDON,LASTUPDATEDON", "@date,@date"));

				cmd.Parameters.AddWithValue("@typeId", edgeType.TypeID);
				cmd.Parameters.AddWithValue("@date", DateTime.Now);
				cmd.Parameters.AddWithValue("@newStatus", (int)IdentityStatus.New);
				cmd.Parameters.AddWithValue("@unchangesStatus", (int)IdentityStatus.Unchanged);

				cmd.ExecuteNonQuery();
			}
		}

		/// <summary>
		/// Update existing EdgeObject by modified objects from Delivery
		/// set Delivery objects to Unchanged after updating EdgeObjects
		/// </summary>
		/// <param name="edgeType"></param>
		private void UpdateExistingEdgeObjectsByDelivery(EdgeType edgeType)
		{
			var updateFieldStr = "LASTUPDATEDON=@lastUpdated,";
			foreach (var field in edgeType.Fields.Where(x => !x.IsIdentity))
			{
				if (!updateFieldStr.Contains(String.Format("{0}=delivery.{0},", field.ColumnNameGK)))
					updateFieldStr = String.Format("{0}{1}=delivery.{1},", updateFieldStr, field.ColumnNameGK);
			}
			if (updateFieldStr.Length == 0) return;

			updateFieldStr = updateFieldStr.Remove(updateFieldStr.Length - 1, 1);
			using (var cmd = new SqlCommand { Connection = _objectsSqlConnection })
			{
				cmd.CommandText = String.Format(@"UPDATE {0} SET {1} FROM {2} delivery 
												  WHERE {0}.GK=delivery.GK AND delivery.IdentityStatus=@modifiedStatus AND delivery.TYPEID=@typeId;
												  UPDATE {2} SET IDENTITYSTATUS=@unchangedStatus WHERE TYPEID=@typeId AND IDENTITYSTATUS=@modifiedStatus",
												edgeType.TableName,
												updateFieldStr,
												GetDeliveryTableName(edgeType.TableName));

				cmd.Parameters.AddWithValue("@typeId", edgeType.TypeID);
				cmd.Parameters.AddWithValue("@lastUpdated", DateTime.Now);
				cmd.Parameters.AddWithValue("@modifiedStatus", (int)IdentityStatus.Modified);
				cmd.Parameters.AddWithValue("@unchangedStatus", (int)IdentityStatus.Unchanged);

				cmd.ExecuteNonQuery();
			}
		}

		/// <summary>
		/// Sync last changes in EdgeObject DB with Delivery using Lock (TODO):
		/// create TEMP table for Delta changes in EdgeObjects, 
		/// update delivery object to unchanged if exists in Delta by identity fields
		/// </summary>
		/// <param name="edgeType"></param>
		private void SyncLastChangesWithLock(EdgeType edgeType)
		{
			var selectStr = String.Empty;
			var whereStr = String.Empty;

			if (!edgeType.Fields.Any(x => x.IsIdentity))
				throw new Exception(String.Format("There are no identity fields defined for type {0}", edgeType.Name));

			foreach (var field in edgeType.Fields.Where(x => x.IsIdentity))
			{
				selectStr = String.Format("{0}{1},", selectStr, field.ColumnNameGK);
				whereStr = String.Format("{0} #DELTA.{1}={2}.{1} AND ", whereStr, field.ColumnNameGK, GetDeliveryTableName(edgeType.TableName));
			}

			selectStr = selectStr.Remove(selectStr.Length - 1, 1);
			whereStr = whereStr.Remove(whereStr.Length - 5, 5);

			using (var cmd = new SqlCommand { Connection = _objectsSqlConnection })
			{
				cmd.CommandText = String.Format(@"SELECT GK,{0} INTO #DELTA FROM {1} WHERE TYPEID=@typeId AND LASTUPDATEDON > @timestamp;
												  UPDATE {2} SET GK=#DELTA.GK, IDENTITYSTATUS=@identityStatus FROM #DELTA WHERE {3};
												  DROP TABLE #DELTA;",
												selectStr, edgeType.TableName, GetDeliveryTableName(edgeType.TableName), whereStr);

				cmd.Parameters.AddWithValue("@typeId", edgeType.TypeID);
				cmd.Parameters.AddWithValue("@timestamp", TransformTimestamp);
				cmd.Parameters.AddWithValue("@identityStatus", (int)IdentityStatus.Unchanged);

				cmd.ExecuteNonQuery();
			}
		}
		#endregion

		#region Logs
		private void PrepareLogCommand()
		{
			if (_logCommand != null) return;

			_logCommand = new SqlCommand
			{
				Connection = _objectsSqlConnection,
				CommandText = @"INSERT INTO [EdgeSystem].[dbo].[Log_v3] ([DateRecorded],[MachineName],[ProcessID],[Source],[ContextInfo],[MessageType],[Message],[ServiceInstanceID],[ServiceProfileID],[IsException],[ExceptionDetails]) 
									VALUES (@dateRecorded, @machineName, @processID, @source, @contextInfo, @messageType, @message, @serviceInstanceID, @serviceProfileID, @isException, @exceptionDetails)"
			};

			_logCommand.Parameters.AddWithValue("@dateRecorded", DateTime.Now);
			_logCommand.Parameters.AddWithValue("@machineName", "(null)");
			_logCommand.Parameters.AddWithValue("@processID", 0);
			_logCommand.Parameters.AddWithValue("@source", "Identity Manager");
			_logCommand.Parameters.AddWithValue("@contextInfo", "(null)");
			_logCommand.Parameters.AddWithValue("@messageType", LogMessageType.Debug);
			_logCommand.Parameters.AddWithValue("@message", String.Empty);
			_logCommand.Parameters.AddWithValue("@serviceInstanceID", DBNull.Value);
			_logCommand.Parameters.AddWithValue("@serviceProfileID", DBNull.Value);
			_logCommand.Parameters.AddWithValue("@isException", false);
			_logCommand.Parameters.AddWithValue("@exceptionDetails", DBNull.Value);
		}

		public void Log(string msg, LogMessageType logType = LogMessageType.Debug)
		{
			if (_logCommand == null) return;

			_logCommand.Parameters["@dateRecorded"].Value = DateTime.Now;
			_logCommand.Parameters["@messageType"].Value = (int)logType;
			_logCommand.Parameters["@message"].Value = msg;
			_logCommand.Parameters["@isException"].Value = logType == LogMessageType.Error;

			_logCommand.ExecuteNonQuery();
		} 
		#endregion
	}
}
