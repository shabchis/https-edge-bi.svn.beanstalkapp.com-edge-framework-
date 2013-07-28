using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using Edge.Data.Objects;
using System.Text;

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
		private IdentityConfig _config;

		public List<EdgeFieldDependencyInfo> Dependencies { get; set; }
		public string TablePrefix { get; set; }
		public int AccountId { get; set; }
		public DateTime TransformTimestamp { get; set; }
		public string ConfigXml { get; set; }

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

			var maxDependecyDepth = Dependencies.Max(x => x.Depth);
			var updatedTypes = new List<EdgeType>();

			for (var i = 0; i <= maxDependecyDepth; i++)
			{
				var currentDepth = i;
				Log(String.Format("IdentifyDeliveryObjects:: dependency depth={0}", currentDepth));

				foreach (var field in Dependencies.Where(x => x.Depth == currentDepth))
				{
					Log(String.Format("IdentifyDeliveryObjects:: starting identify field '{0}' of type '{1}'", field.Field.Name, field.Field.FieldEdgeType.Name));

					// nothing to do if field of the same type was already updated
					if (updatedTypes.Contains(field.Field.FieldEdgeType))
					{
						Log(String.Format("IdentifyDeliveryObjects:: nothing to Identify for field '{0}', type '{1}' was already identified", field.Field.Name, field.Field.FieldEdgeType.Name));
						continue;
					}

					Log(String.Format("IdentifyDeliveryObjects:: update '{0}' type dependencies", field.Field.FieldEdgeType.Name));
					UpdateObjectDependencies(field.Field.FieldEdgeType);

					Log(String.Format("IdentifyDeliveryObjects:: Create Temp Objects table for type '{0}'", field.Field.FieldEdgeType.Name));
					CreateTempObjectsTable(field.Field.FieldEdgeType);

					Log(String.Format("IdentifyDeliveryObjects:: Set delivery objects identity (GK) for type '{0}'", field.Field.FieldEdgeType.Name));
					SetIdentity(field.Field.FieldEdgeType);

					Log(String.Format("IdentifyDeliveryObjects:: Create Temp Delivery GK-TK table for type '{0}'", field.Field.FieldEdgeType.Name));
					CreateTempDeliveryGkTkTable(field.Field.FieldEdgeType);

					Log(String.Format("IdentifyDeliveryObjects:: Finished identify type '{0}'", field.Field.FieldEdgeType.Name));
					updatedTypes.Add(field.Field.FieldEdgeType);
				}
			}
		}

		/// <summary>
		/// Set identity of Delivery objects by edge type:
		/// update GK according to identity fields against temp object table
		/// set identity status=1 (unchanged) if non-identity fields were NOT changed
		/// set identity status=2 (modified) if non-identity fields WERE changed
		/// </summary>
		private void SetIdentity(EdgeType edgeType)
		{
			var identityFieldsSb = new StringBuilder();
			var fieldsSb = new StringBuilder();

			foreach (var field in edgeType.Fields)
			{
				if (field.IsIdentity)
					identityFieldsSb.AppendFormat("{0}delivery.{1}=temp.{1}", identityFieldsSb.Length > 0 ? " AND " : "", field.ColumnNameGK);
				else
					fieldsSb.AppendFormat("{0}delivery.{1}=temp.{1}", fieldsSb.Length > 0 ? " AND " : "", field.ColumnNameGK);
			}
			if (identityFieldsSb.Length == 0)
				throw new Exception(String.Format("No identity fields are defined for type '{0}'", edgeType.Name));

			var sql = String.Format(@"UPDATE {0} 
										SET Gk = temp.Gk, IdentityStatus = {1} 
										FROM {0} delivery, ##TempEdgeObject_{2} temp
										WHERE delivery.TypeId = @typeId AND {3}", 
							GetDeliveryTableName(edgeType.TableName),
							fieldsSb.Length > 0 ? String.Format("CASE WHEN {0} THEN 1 ELSE 2 END", fieldsSb) : "1",
							edgeType.Name,
							identityFieldsSb
						);

			Log(String.Format("IdentifyDeliveryObjects:: Identity type '{0}' SQL: {1}", edgeType.Name, sql));

			using(var cmd = new SqlCommand {Connection = _objectsSqlConnection, CommandText = sql})
			{
				cmd.Parameters.Add(new SqlParameter("@typeId", edgeType.TypeID));
				cmd.ExecuteNonQuery();
			}
		}

		/// <summary>
		/// Create temporary table by edge type from original EdgeObjects table indexed by identity fields
		/// </summary>
		private void CreateTempObjectsTable(EdgeType edgeType)
		{
			if (!edgeType.Fields.Any(x => x.IsIdentity))
				throw new Exception(String.Format("There are no identity fields defined for type {0}", edgeType.Name));

			var selectColumnStr = "GK,";
			var indexFieldsStr = String.Empty;
			var tempObjectTableName = String.Format("##TempEdgeObject_{0}", edgeType.Name);

			foreach (var field in edgeType.Fields)
			{
				if (selectColumnStr.Contains(String.Format("{0},", field.ColumnNameGK))) continue;

				// add columns to SELECT (later check if edge object was updated or not)
				selectColumnStr = String.Format("{0}{1},", selectColumnStr, field.ColumnNameGK);
				if (field.IsIdentity)
					indexFieldsStr = String.Format("{0}{1},", indexFieldsStr, field.ColumnNameGK);
			}

			selectColumnStr = selectColumnStr.Remove(selectColumnStr.Length - 1, 1);
			indexFieldsStr = indexFieldsStr.Remove(indexFieldsStr.Length - 1, 1);

			using (var createTempTableCmd = new SqlCommand {Connection = _objectsSqlConnection})
			{
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
				createTempTableCmd.Parameters.Add(new SqlParameter("@typeId", edgeType.TypeID));
				createTempTableCmd.ExecuteNonQuery();
			}
		}

		/// <summary>
		/// Create temporary table which contains GK, TK mapping of updated object type in Delivery
		/// </summary>
		private void CreateTempDeliveryGkTkTable(EdgeType edgeType)
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
		/// Before searching for object GKs update all GK of its parent fields 
		/// (objects it depends on)
		/// For example: before searching for AdGroup GKs, update all AdGroup Campaings
		/// </summary>
		private void UpdateObjectDependencies(EdgeType edgeType)
		{
			// nothitng to do if there are no GK to update
			if (edgeType.Fields.All(x => x.Field.FieldEdgeType == null)) return;

			var mainTableName = GetDeliveryTableName(edgeType.TableName);
			var paramList = new List<SqlParameter> { new SqlParameter("@typeId", edgeType.TypeID) };
			var cmdStr = String.Empty;

			foreach (var parentField in edgeType.Fields.Where(x => x.Field.FieldEdgeType != null))
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
			Log(String.Format("UpdateObjectDependencies:: dependencies updated for edge type '{0}'", edgeType.Name));
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
			_config = String.IsNullOrEmpty(ConfigXml) ? new IdentityConfig() : IdentityConfig.Deserialize(ConfigXml);

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
					Log(String.Format("UpdateEdgeObjects:: starting update field '{0}' of type '{1}'", field.Field.Name, field.Field.FieldEdgeType.Name));
					UpdateObjectDependencies(field.Field.FieldEdgeType);

					if (_config.UpdateExistingObjects && DeliveryContainsChanges(field.Field.FieldEdgeType, IdentityStatus.Unchanged, true))
					{
						Log(String.Format("UpdateEdgeObjects:: delivery contains changes of {0}, starting sync", field.Field.Name));
						
						SyncLastChangesWithLock(field.Field.FieldEdgeType);

						if (DeliveryContainsChanges(field.Field.FieldEdgeType, IdentityStatus.Modified))
						{
							UpdateExistingEdgeObjectsByDelivery(field.Field.FieldEdgeType);
							Log(String.Format("UpdateEdgeObjects:: update modified object of type '{0}'", field.Field.FieldEdgeType));
						}

						if (_config.CreateNewObjects && DeliveryContainsChanges(field.Field.FieldEdgeType, IdentityStatus.New))
						{
							InsertNewEdgeObjects(field.Field.FieldEdgeType);
							Log(String.Format("UpdateEdgeObjects:: insert new objects of type '{0}'", field.Field.FieldEdgeType));
						}
					}
					else
						Log(String.Format("UpdateEdgeObjects:: delivery doen't contain changes of {0}, nothing to sync", field.Field.Name));

					CreateTempDeliveryGkTkTable(field.Field.FieldEdgeType);
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

			// check if configured to update fields
			var typeConfig = _config.EdgeTypes.FirstOrDefault(x => x.Name == edgeType.Name);
			if (typeConfig != null && typeConfig.CreateNewObjects == false)
			{
				Log(String.Format("Insert new objects into Staging is not performed for type '{0}' according to IdentityConfig", edgeType.Name));
				return;
			}

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
			// check if configured to update fields
			var typeConfig = _config.EdgeTypes.FirstOrDefault(x => x.Name == edgeType.Name);
			if (typeConfig != null && typeConfig.UpdateExistingObjects == false)
			{
				Log(String.Format("Update existing objects in Staging is not performed for type '{0}' according to IdentityConfig", edgeType.Name));
				return;
			}
			// indication if t update all fields otr just a few of them
			var updateAllFields = typeConfig == null || typeConfig.Fields.Count == 0;

			var updateFieldStr = "LASTUPDATEDON=@lastUpdated,";
			foreach (var field in edgeType.Fields.Where(x => !x.IsIdentity))
			{
				// update all fields or only fields configured to be updated
				if (updateAllFields || typeConfig.Fields.Any(x => x.Name == field.Field.Name))
				{
					if (!updateFieldStr.Contains(String.Format("{0}=delivery.{0},", field.ColumnNameGK)))
						updateFieldStr = String.Format("{0}{1}=delivery.{1},", updateFieldStr, field.ColumnNameGK);
				}
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
