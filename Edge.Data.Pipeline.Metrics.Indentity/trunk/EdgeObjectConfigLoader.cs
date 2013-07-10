using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using Edge.Data.Objects;

namespace Edge.Data.Pipeline.Metrics.Indentity
{
	/// <summary>
	/// Helper to load configuration from EdgeObjects DB 
	/// (edge types, edge fields, relations between them, measures, etc.)
	/// </summary>
	public static class EdgeObjectConfigLoader
	{
		#region Public Methods

		/// <summary>
		/// Load specific account or all accounts if account id = -1
		/// </summary>
		public static Dictionary<string, Account> LoadAccounts(int accountId, SqlConnection connection)
		{
			var accounts = new Dictionary<string, Account>();
			var accountMap = new Dictionary<int, KeyValuePair<Account, int>>();

			try
			{
				// load all accounts
				using (var cmd = new SqlCommand("SELECT Id, Name, ParentAccountID FROM [EdgeObjects].[dbo].[Account] WHERE Status=1", connection))
				{
					using (var reader = cmd.ExecuteReader())
					{
						while (reader.Read())
						{
							var account = new Account
							{
								ID = int.Parse(reader["ID"].ToString()),
								Name = reader["Name"].ToString()
							};
							accountMap.Add(account.ID, new KeyValuePair<Account, int>(account, reader["ParentAccountID"] == DBNull.Value ? 0 : int.Parse(reader["ParentAccountID"].ToString())));
						}
					}
				}
				// set recursively all parent accounts of current account (till Parent Id == 0)
				if (accountMap.ContainsKey(accountId))
				{
					var account = accountMap[accountId].Key;
					var parentId = accountMap[accountId].Value;
					SetParentAccount(account, parentId, accountMap);
					accounts.Add(account.Name, account);
				}
				else
					throw new Exception(String.Format("Account {0} is not found or is inactive", accountId));

			}
			catch (Exception ex)
			{
				throw new Exception("Error while trying to get accounts from DB", ex);
			}
			return accounts;
		}

		/// <summary>
		/// Load channels by account (all if account id = -1)
		/// </summary>
		/// <returns></returns>
		public static Dictionary<string, Channel> LoadChannels(SqlConnection connection)
		{
			var channels = new Dictionary<string, Channel>(StringComparer.CurrentCultureIgnoreCase);
			try
			{
				using (var cmd = new SqlCommand("[EdgeObjects].[dbo].[Channel_Get]", connection))
				{
					cmd.CommandType = CommandType.StoredProcedure;
					using (var reader = cmd.ExecuteReader())
					{
						while (reader.Read())
						{
							var channel = new Channel
							{
								ID = int.Parse(reader["ID"].ToString()),
								Name = reader["Name"].ToString()
							};
							channels.Add(channel.Name, channel);
						}
					}
				}
			}
			catch (Exception ex)
			{
				throw new Exception("Error while trying to get Channels from DB", ex);
			}
			return channels;
		}

		/// <summary>
		/// Load measures by account
		/// </summary>
		public static Dictionary<string, Measure> LoadMeasures(int accountId, SqlConnection connection)
		{
			var measures = new Dictionary<string, Measure>();
			try
			{
				using (var cmd = new SqlCommand("[EdgeObjects].[dbo].[MD_Measure_Get]", connection))
				{
					cmd.Parameters.AddWithValue("@accountID", accountId);
					cmd.CommandType = CommandType.StoredProcedure;

					using (var reader = cmd.ExecuteReader())
					{
						while (reader.Read())
						{
							var measure = new Measure
							{
								ID = int.Parse(reader["ID"].ToString()),
								Name = reader["Name"].ToString(),
								DataType = reader["DataType"] != DBNull.Value ? (MeasureDataType)int.Parse(reader["DataType"].ToString()) : MeasureDataType.Number,
								//InheritedByDefault = reader["InheritedByDefault"] != DBNull.Value && bool.Parse(reader["InheritedByDefault"].ToString()),
								Options = reader["Options"] != DBNull.Value ? (MeasureOptions)int.Parse(reader["Options"].ToString()) : MeasureOptions.None,
							};
							measures.Add(measure.Name, measure);
						}
					}
				}
			}
			catch (Exception ex)
			{
				throw new Exception("Error while trying to get measures from DB", ex);
			}
			return measures;
		}

		/// <summary>
		/// load edge types by account
		/// </summary>
		public static Dictionary<string, EdgeType> LoadEdgeTypes(int accountId, SqlConnection connection)
		{
			var assemblyFullName = typeof(EdgeType).Assembly.FullName;

			var edgeTypes = new Dictionary<string, EdgeType>();
			try
			{
				using (var cmd = new SqlCommand("[EdgeObjects].[dbo].[MD_EdgeType_Get]", connection))
				{
					cmd.Parameters.AddWithValue("@accountID", accountId);
					cmd.CommandType = CommandType.StoredProcedure;

					using (var reader = cmd.ExecuteReader())
					{
						while (reader.Read())
						{
							var type = new EdgeType
							{
								TypeID = int.Parse(reader["TypeID"].ToString()),
								Name = reader["Name"].ToString(),
								TableName = reader["TableName"].ToString(),
								ClrType = Type.GetType(String.Format("{0}, {1}", reader["ClrType"], assemblyFullName)), // TODO: check how to get type from class & dll name
								IsAbstract = bool.Parse(reader["IsAbstract"].ToString())
							};
							int baseTypeId;
							if (int.TryParse(reader["BaseTypeID"].ToString(), out baseTypeId))
								type.BaseEdgeType = edgeTypes.Values.FirstOrDefault(x => x.TypeID == baseTypeId);
							edgeTypes.Add(type.Name, type);
						}
					}
				}
			}
			catch (Exception ex)
			{
				throw new Exception("Error while trying to get edge types from DB", ex);
			}
			return edgeTypes;
		}

		/// <summary>
		/// Load edge fields by account + set edge type for each field
		/// </summary>
		public static List<EdgeField> LoadEdgeFields(int accountId, Dictionary<string, EdgeType> edgeTypes, SqlConnection connection)
		{
			var edgeFields = new List<EdgeField>();
			try
			{
				using (var cmd = new SqlCommand("[EdgeObjects].[dbo].[MD_EdgeField_Get]", connection))
				{
					cmd.Parameters.AddWithValue("@accountID", accountId);
					cmd.CommandType = CommandType.StoredProcedure;

					using (var reader = cmd.ExecuteReader())
					{
						while (reader.Read())
						{
							EdgeField field;
							Type type = Type.GetType(reader["FieldType"].ToString());
							if (type == null) 
								field = new SystemField();
							else
								field = Activator.CreateInstance(type) as EdgeField;

							if (field != null)
							{
								field.FieldID = int.Parse(reader["FieldID"].ToString());
								field.Name = reader["Name"].ToString();
								field.DisplayName = reader["DisplayName"].ToString();
								field.FieldEdgeType = edgeTypes.Values.FirstOrDefault(x => x.TypeID == int.Parse(reader["FieldTypeID"].ToString()));

								edgeFields.Add(field);
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				throw new Exception("Error while trying to get extra fields from DB", ex);
			}
			return edgeFields;
		}

		/// <summary>
		/// Set relationships between edge fields and edge types (many-2-many) according to account id
		/// </summary>
		public static void SetEdgeTypeEdgeFieldRelation(int accountId, Dictionary<string, EdgeType> edgeTypes, List<EdgeField> edgeFields, SqlConnection connection)
		{
			try
			{
				using (var cmd = new SqlCommand("[EdgeObjects].[dbo].[MD_EdgeTypeField_Get]", connection))
				{
					cmd.Parameters.AddWithValue("@accountID", accountId);
					cmd.CommandType = CommandType.StoredProcedure;

					using (var reader = cmd.ExecuteReader())
					{
						while (reader.Read())
						{
							// find parent edge type nad edge field
							var parentTypeId = int.Parse(reader["ParentTypeID"].ToString());
							var fieldtId = int.Parse(reader["FieldID"].ToString());
							// TODO: Amit to remove COLLATE Hebrew_CI_AS (DB definition)
							var clmLength = reader["ColumnLength"] == DBNull.Value ? String.Empty : String.Format("({0}) COLLATE Hebrew_CI_AS", reader["ColumnLength"]);

							var parentType = edgeTypes.Values.FirstOrDefault(x => x.TypeID == parentTypeId);
							if (parentType == null)
								throw new Exception(String.Format("Configuration error: Unknown parent edge type {0} while loading edge type fields", parentTypeId));

							var field = edgeFields.FirstOrDefault(x => x.FieldID == fieldtId);
							if (field == null)
								throw new Exception(String.Format("Configuration error: Unknown edge field {0} while loading edge type fields", fieldtId));

							var typeField = new EdgeTypeField
							{
								ColumnName = reader["ColumnName"].ToString(),
								ColumnDbType = String.Format("{0}{1}", reader["ColumnType"], clmLength),
								IsIdentity = bool.Parse(reader["IsIdentity"].ToString()),
								Field = field
							};

							// add edge field to parent edge type
							if (!parentType.Fields.Contains(typeField))
								parentType.Fields.Add(typeField);
							else
								throw new Exception(String.Format("Configuration error: Field {0} already exists in parent edge type {1}", field.Name, parentType.Name));

						}
					}
				}
			}
			catch (Exception ex)
			{
				throw new Exception("Error while trying to get extra fields from DB", ex);
			}
		}

		/// <summary>
		/// Build dependencies of edge objects: 
		/// for each edge field: dependency depth level, list of parents with relevant column name and indication for identity
		/// </summary>
		public static Dictionary<EdgeField, EdgeFieldDependencyInfo> GetEdgeObjectDependencies(int accountId, SqlConnection connection)
		{
			var edgeTypes = LoadEdgeTypes(accountId, connection);
			var edgeFields = LoadEdgeFields(accountId, edgeTypes, connection);
			SetEdgeTypeEdgeFieldRelation(accountId, edgeTypes, edgeFields, connection);

			// build object dependencies: child with parent list
			var dependencies = edgeFields.Where(f => f.FieldEdgeType != null).ToDictionary(f => f, f => new EdgeFieldDependencyInfo { Field = f });
			foreach (var field in dependencies.Values)
			{
				FindFieldDependencies(field.Field, dependencies, edgeFields);
			}

			// set dependency depth of each object 
			foreach (var field in edgeFields.Where(x => x.FieldEdgeType != null))
			{
				dependencies[field].Depth = SetFieldDependencyDepth(field, edgeFields);
			}
			return dependencies;
		}

		/// <summary>
		/// Find all inheritors (child types) for source edge type for flat Metrics and Objects tables
		/// except of absract types
		/// </summary>
		/// <param name="sourceType"></param>
		/// <param name="edgeTypes"></param>
		/// <returns></returns>
		public static IList<EdgeType> FindEdgeTypeInheritors(EdgeType sourceType, Dictionary<string, EdgeType> edgeTypes)
		{
			var outList = new List<EdgeType>();
			AddTypeInheritorsRecursively(sourceType, edgeTypes, outList);
			return outList;
		}

		private static void AddTypeInheritorsRecursively(EdgeType edgeType, Dictionary<string, EdgeType> edgeTypes, ICollection<EdgeType> list)
		{
			if (!list.Contains(edgeType) && !edgeType.IsAbstract) list.Add(edgeType);
			foreach (var currType in edgeTypes.Values.Where(x => x.BaseEdgeType != null && x.BaseEdgeType.TypeID == edgeType.TypeID))
			{
				AddTypeInheritorsRecursively(currType, edgeTypes, list);
			}
		}
		#endregion

		#region Private Methods
		private static int SetFieldDependencyDepth(EdgeField field, List<EdgeField> edgeFields)
		{
			var maxDepth = 0;
			foreach (var childField in field.FieldEdgeType.Fields)
			{
				if (childField.Field.FieldEdgeType != null)
				{
					if (childField.Field.FieldEdgeType.IsAbstract)
					{
						foreach (var inheritor in GetInheritorsByType(childField.Field.FieldEdgeType.TypeID, edgeFields))
						{
							var childDepth = SetFieldDependencyDepth(inheritor, edgeFields);
							maxDepth = maxDepth > childDepth + 1 ? maxDepth : childDepth + 1;
						}
					}
					else
					{
						var childDepth = SetFieldDependencyDepth(childField.Field, edgeFields);
						maxDepth = maxDepth > childDepth + 1 ? maxDepth : childDepth + 1;
					}
				}
			}
			return maxDepth;
		}

		private static IEnumerable<EdgeField> GetInheritorsByType(int typeId, IEnumerable<EdgeField> edgeFields)
		{
			return edgeFields.Where(x => x.FieldEdgeType != null && x.FieldEdgeType.BaseEdgeType != null &&
			                             x.FieldEdgeType.BaseEdgeType.TypeID == typeId);
		}

		private static void FindFieldDependencies(EdgeField field, Dictionary<EdgeField, EdgeFieldDependencyInfo> dependencies, List<EdgeField> edgeFields)
		{
			foreach (var childField in field.FieldEdgeType.Fields.Where(x => x.Field.FieldEdgeType != null))
			{
				if (!dependencies[childField.Field].DependentFields.ContainsKey(field))
				{
					if (childField.Field.FieldEdgeType.IsAbstract)
					{
						// take care of edge types inheritance
						foreach (var inheritor in GetInheritorsByType(childField.Field.FieldEdgeType.TypeID, edgeFields))
						{
							dependencies[inheritor].DependentFields.Add(field, new EdgeTypeField
							{
								Field = field,
								ColumnName = childField.ColumnName,
								IsIdentity = childField.IsIdentity
							});
							FindFieldDependencies(inheritor, dependencies, edgeFields);
						}
					}
					dependencies[childField.Field].DependentFields.Add(field, new EdgeTypeField
																		{
																			Field = field,
																			ColumnName = childField.ColumnName,
																			IsIdentity = childField.IsIdentity
																		});
					FindFieldDependencies(childField.Field, dependencies, edgeFields);
				}
			}
		}

		/// <summary>
		/// Set recursively parent account to specific account according to received Accounts Map
		/// </summary>
		private static void SetParentAccount(Account account, int parentId, Dictionary<int, KeyValuePair<Account, int>> accountMap)
		{
			// no parent
			if (parentId == 0) return;

			// parent is already set - to avoid circling
			if (account.ParentAccount != null) return;

			if (accountMap.ContainsKey(parentId))
			{
				var parentAccount = accountMap[parentId].Key;
				var grantPaId = accountMap[parentId].Value;
				account.ParentAccount = parentAccount;

				SetParentAccount(parentAccount, grantPaId, accountMap);
			}
			else
				throw new Exception(String.Format("Account {0} is not found or is inactive", parentId));
		}
		#endregion
	}
}
