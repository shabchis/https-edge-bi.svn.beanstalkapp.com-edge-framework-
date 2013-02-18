using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using Edge.Core.Configuration;
using Edge.Core.Utilities;
using Edge.Data.Objects;
using Edge.Data.Pipeline.Mapping;
using Edge.Data.Pipeline.Metrics.Managers;
using Edge.Data.Pipeline.Metrics.Misc;
using Edge.Data.Pipeline.Objects;
using Edge.Data.Pipeline.Services;

namespace Edge.Data.Pipeline.Metrics.Services
{
	/// <summary>
	/// Base metrics processor service
	/// </summary>
	public abstract class MetricsProcessorServiceBase : PipelineService
	{
		#region Properties
		public Dictionary<string, Account>    Accounts { get; private set; }
		public Dictionary<string, Channel>    Channels { get; private set; }
		public Dictionary<string, Measure>    Measures { get; private set; }
		public Dictionary<string, EdgeType>   EdgeTypes { get; private set; }
		public List<ExtraField> ExtraFields { get; private set; }

		public MetricsDeliveryManager ImportManager { get; protected set; }
		private int _accountId = -1;
		protected MetricsUnit CurrentMetricsUnit;

		#endregion

		#region Mappings
		protected virtual void InitMappings()
		{
			if (Configuration.Parameters["AccountID"] != null)
			{
				int.TryParse(Configuration.Parameters["AccountID"].ToString(), out _accountId);
			}

			// load definitions from DB
			LoadAccounts();
			LoadChannels();
			LoadMeasures();
			LoadEdgeTypes();
			LoadEdgeFields();
			LoadEdgeTypeFields();

			// Load mapping configuration
			Mappings.ExternalMethods.Add("GetChannel", new Func<dynamic, Channel>(GetChannel));
			Mappings.ExternalMethods.Add("GetCurrentChannel", new Func<Channel>(GetCurrentChannel));
			Mappings.ExternalMethods.Add("GetAccount", new Func<dynamic, Account>(GetAccount));
			Mappings.ExternalMethods.Add("GetCurrentAccount", new Func<Account>(GetCurrentAccount));
			Mappings.ExternalMethods.Add("GetExtraField", new Func<dynamic, ExtraField>(GetExtraField));
			Mappings.ExternalMethods.Add("GetCompositePartField", new Func<dynamic, CompositePartField>(GetCompositePartField));
			Mappings.ExternalMethods.Add("GetTargetField", new Func<dynamic, TargetField>(GetTargetField));
			Mappings.ExternalMethods.Add("GetEdgeType", new Func<dynamic, EdgeType>(GetEdgeType));
			Mappings.ExternalMethods.Add("GetMeasure", new Func<dynamic, Measure>(GetMeasure));
			Mappings.ExternalMethods.Add("GetObjectByEdgeType", new Func<dynamic, EdgeObject>(GetObjectByEdgeType));
			Mappings.ExternalMethods.Add("GetObjectByEdgeTypeAndEdgeField", new Func<dynamic, dynamic, EdgeObject>(GetObjectByEdgeTypeAndEdgeField));
			Mappings.ExternalMethods.Add("CreatePeriodStart", new Func<dynamic, dynamic, dynamic, DateTime>(CreatePeriodStart));
			Mappings.ExternalMethods.Add("CreatePeriodEnd", new Func<dynamic, dynamic, dynamic, DateTime>(CreatePeriodEnd));
			
			Mappings.Compile();
		}
 
		#endregion

		#region Scriptable methods
		// ==============================================

		public Account GetAccount(dynamic name)
		{
			var n = (string)name;
			Account a;
			if (!Accounts.TryGetValue(n, out a))
				throw new MappingException(String.Format("No account named '{0}' could be found, or it cannot be used from within account #{1}.", n, _accountId));
			return a;
		}

		public Account GetCurrentAccount()
		{
			return new Account { ID = _accountId };
		}

		public Channel GetChannel(dynamic name)
		{
			var n = (string)name;
			Channel c;
			if (!Channels.TryGetValue(n, out c))
				throw new MappingException(String.Format("No channel named '{0}' could be found.", n));
			return c;
		}

		public Channel GetCurrentChannel()
		{
			return Delivery.Channel;
		}

		public EdgeType GetEdgeType(dynamic name)
		{
			var n = (string)name;
			EdgeType type;
			if (!EdgeTypes.TryGetValue(n, out type))
				throw new MappingException(String.Format("No edge type named '{0}' could be found.", n));
			return type;
		}

		public ExtraField GetExtraField(dynamic fieldName)
		{
			var strFieldName = (string) fieldName;

			var field = ExtraFields.FirstOrDefault(x => x.Name == strFieldName);
			if (field == null)
				throw new MappingException(String.Format("Unknown edge field '{0}'", strFieldName));
			return field;
		}

		public CompositePartField GetCompositePartField(dynamic fieldName)
		{
			var field = GetExtraField(fieldName);
			return new CompositePartField
				{
					FieldID = field.FieldID,
					Name = field.Name,
					DisplayName = field.DisplayName,
					FieldEdgeType = field.FieldEdgeType
				};
		}

		public TargetField GetTargetField(dynamic fieldName)
		{
			var field = GetExtraField(fieldName);
			return new TargetField
			{
				FieldID = field.FieldID,
				Name = field.Name,
				DisplayName = field.DisplayName,
				FieldEdgeType = field.FieldEdgeType
			};
		}

		public Measure GetMeasure(dynamic name)
		{
			var n = (string)name;
			Measure m;
			if (!Measures.TryGetValue(n, out m))
				throw new MappingException(String.Format("No measure named '{0}' could be found. Make sure you specified the base measure name, not the display name.", n));
			return m;
		}

		public EdgeObject GetObjectByEdgeType(dynamic edgeType)
		{
			return GetObjectByEdgeTypeAndEdgeField(edgeType, String.Empty);
		}

		/// <summary>
		/// try to find specific object edge in currrent metrics unit by type (not EdgeType, because it will set after mapping) 
		/// and field name (optional, the 1st occurancy is returned
		/// </summary>
		/// <param name="edgeType">CLR type name</param>
		/// <param name="edgeField">EdgeField name</param>
		/// <returns>EdgeObject or NULL if not found</returns>
		public EdgeObject GetObjectByEdgeTypeAndEdgeField(dynamic edgeType, dynamic edgeField)
		{
			var typeName = (string)edgeType;
			var fieldName = (string)edgeField;
			if (CurrentMetricsUnit == null)
				throw new MappingException("Current metrics unit is NULL");

			foreach (var dimension in CurrentMetricsUnit.GetObjectDimensions().Where(x => x.Value is EdgeObject))
			{
				return GetObjectRecursively(typeName, fieldName, dimension);
			}
			return null;
		}

		private EdgeObject GetObjectRecursively(string typeName, string fieldName, ObjectDimension currentDimension)
		{
			var edgeObj = currentDimension.Value as EdgeObject;
			if (edgeObj == null) return null;

			if (edgeObj.GetType().Name == typeName && (String.IsNullOrEmpty(fieldName) || currentDimension.Field != null && currentDimension.Field.Name == fieldName))
			{
				// found the required object by type and edge file
				return edgeObj;
			}
			foreach (var childDimension in edgeObj.GetObjectDimensions().Where(x => x.Value is EdgeObject))
			{
				var returnObj = GetObjectRecursively(typeName, fieldName, childDimension);
				if (returnObj != null) return returnObj;
			}
			return null;
		}

		public DateTime CreatePeriodStart(dynamic year, dynamic month, dynamic day)
		{
			return CreatePeriod(DateTimeSpecificationAlignment.Start, (string)year, (string)month, (string)day);
		}

		public DateTime CreatePeriodEnd(dynamic year, dynamic month, dynamic day)
		{
			return CreatePeriod(DateTimeSpecificationAlignment.End, (string)year, (string)month, (string)day);
		}

		public DateTime CreatePeriod(DateTimeSpecificationAlignment align, string year, string month, string day)//, string hour = null, string minute = null, string second = null )
		{
			DateTime baseDateTime;
			try { baseDateTime = new DateTime(Int32.Parse(year), Int32.Parse(month), Int32.Parse(day)); }
			catch (Exception ex)
			{
				throw new MappingException(String.Format("Could not parse the date parts (y = '{0}', m = '{1}', d = '{2}'.", year, month, day), ex);
			}

			DateTime period = new DateTimeSpecification
				{
					Alignment = align,
					BaseDateTime = baseDateTime
				}
				.ToDateTime();

			return period;
		}

		// ==============================================
		#endregion

		#region Private Methods
		private void LoadAccounts()
		{
			Accounts = new Dictionary<string, Account>();
			try
			{
				using (var connection = new SqlConnection(AppSettings.GetConnectionString(typeof(MetricsProcessorServiceBase), Consts.ConnectionStrings.Objects)))
				{
					var cmd = SqlUtility.CreateCommand("Account_Get", CommandType.StoredProcedure);
					cmd.Parameters.AddWithValue("@accountID", _accountId); 
					cmd.Connection = connection;
					connection.Open();

					using (var reader = cmd.ExecuteReader())
					{
						while (reader.Read())
						{
							var account = new Account
								{
									ID = int.Parse(reader["ID"].ToString()),
									Name = reader["Name"].ToString()
								};
							Accounts.Add(account.Name, account);
						}
					}
				}
			}
			catch (Exception ex)
			{
				throw new Exception("Error while trying to get accounts from DB", ex);
			}
		}

		private void LoadChannels()
		{
			Channels = new Dictionary<string, Channel>(StringComparer.CurrentCultureIgnoreCase);
			try
			{
				using (var connection = new SqlConnection(AppSettings.GetConnectionString(typeof(MetricsProcessorServiceBase), Consts.ConnectionStrings.Objects)))
				{
					var cmd = SqlUtility.CreateCommand("Channel_Get", CommandType.StoredProcedure);
					cmd.Connection = connection;
					connection.Open();
					using (var reader = cmd.ExecuteReader())
					{
						while (reader.Read())
						{
							var channel = new Channel
								{
									ID = int.Parse(reader["ID"].ToString()),
									Name = reader["Name"].ToString()
								};
							Channels.Add(channel.Name, channel);
						}
					}
				}
			}
			catch (Exception ex)
			{
				throw new Exception("Error while trying to get Channels from DB", ex);
			}
		}

		private void LoadMeasures()
		{
			Measures = new Dictionary<string, Measure>();
			try
			{
				using (var connection = new SqlConnection(AppSettings.GetConnectionString(typeof(MetricsProcessorServiceBase), Consts.ConnectionStrings.Objects)))
				{
					var cmd = SqlUtility.CreateCommand("MD_Measure_Get", CommandType.StoredProcedure);
					cmd.Parameters.AddWithValue("@accountID", _accountId);
					cmd.Connection = connection;
					connection.Open();

					using (var reader = cmd.ExecuteReader())
					{
						while (reader.Read())
						{
							var measure = new Measure
							{
								ID = int.Parse(reader["ID"].ToString()),
								Name = reader["Name"].ToString(),
								DataType = reader["MeasureDataType"] != DBNull.Value ? (MeasureDataType)int.Parse(reader["MeasureDataType"].ToString()) : MeasureDataType.Number,
								InheritedByDefault = reader["InheritedByDefault"] != DBNull.Value && bool.Parse(reader["InheritedByDefault"].ToString()),
								Options = reader["Options"] != DBNull.Value ? (MeasureOptions)int.Parse(reader["Options"].ToString()) : MeasureOptions.None,
							};
							Measures.Add(measure.Name, measure);
						}
					}
				}
			}
			catch (Exception ex)
			{
				throw new Exception("Error while trying to get measures from DB", ex);
			}
		}

		private void LoadEdgeTypes()
		{
 			EdgeTypes = new Dictionary<string, EdgeType>();
			try
			{
				using (var connection = new SqlConnection(AppSettings.GetConnectionString(typeof(MetricsProcessorServiceBase), Consts.ConnectionStrings.Objects)))
				{
					var cmd = SqlUtility.CreateCommand("MD_EdgeType_Get", CommandType.StoredProcedure);
					cmd.Parameters.AddWithValue("@accountID", _accountId);
					cmd.Connection = connection;
					connection.Open();

					using (var reader = cmd.ExecuteReader())
					{
						while (reader.Read())
						{
							var type = new EdgeType
							{
								TypeID = int.Parse(reader["TypeID"].ToString()),
								Name = reader["Name"].ToString(),
								TableName = reader["TableName"].ToString(),
								ClrType = Type.GetType(reader["ClrType"].ToString())
							};
							EdgeTypes.Add(type.Name, type);
						}
					}
				}
			}
			catch (Exception ex)
			{
				throw new Exception("Error while trying to get edge types from DB", ex);
			}
		}

		private void LoadEdgeFields()
		{
			ExtraFields = new List<ExtraField>();
			try
			{
				using (var connection = new SqlConnection(AppSettings.GetConnectionString(typeof(MetricsProcessorServiceBase), Consts.ConnectionStrings.Objects)))
				{
					var cmd = SqlUtility.CreateCommand("MD_EdgeField_Get", CommandType.StoredProcedure);
					cmd.Parameters.AddWithValue("@accountID", _accountId);
					cmd.Connection = connection;
					connection.Open();

					using (var reader = cmd.ExecuteReader())
					{
						while (reader.Read())
						{
							var field = new ExtraField
							{
								FieldID = int.Parse(reader["FieldID"].ToString()),
								Name = reader["Name"].ToString(),
								DisplayName = reader["DisplayName"].ToString(),
								FieldEdgeType = EdgeTypes.Values.FirstOrDefault(x => x.TypeID == int.Parse(reader["FieldTypeID"].ToString())),
							};
							ExtraFields.Add(field);
						}
					}
				}
			}
			catch (Exception ex)
			{
				throw new Exception("Error while trying to get extra fields from DB", ex);
			}
		}

		private void LoadEdgeTypeFields()
		{
			try
			{
				using (var connection = new SqlConnection(AppSettings.GetConnectionString(typeof(MetricsProcessorServiceBase), Consts.ConnectionStrings.Objects)))
				{
					var cmd = SqlUtility.CreateCommand("MD_EdgeTypeField_Get", CommandType.StoredProcedure);
					cmd.Parameters.AddWithValue("@accountID", _accountId);
					cmd.Connection = connection;
					connection.Open();

					using (var reader = cmd.ExecuteReader())
					{
						while (reader.Read())
						{
							// find parent edge type nad edge field
							var parentTypeId = int.Parse(reader["ParentTypeID"].ToString());
							var fieldtId = int.Parse(reader["FieldID"].ToString());

							var parentType = EdgeTypes.Values.FirstOrDefault(x => x.TypeID == parentTypeId);
							if (parentType == null)
								throw new ConfigurationErrorsException(String.Format("Configuration error: Unknown parent edge type {0} while loading edge type fields", parentTypeId));
							
							var field = ExtraFields.FirstOrDefault(x => x.FieldID == fieldtId);
							if (field == null)
								throw new ConfigurationErrorsException(String.Format("Configuration error: Unknown edge field {0} while loading edge type fields", fieldtId));

							var typeField = new EdgeTypeField
									{
										ColumnName   = reader["ColumnName"].ToString(),
										IsIdentity	   = reader["IsIdentity"].ToString() == "1",
										Field = field
									};

							// add edge field to parent edge type
							if (!parentType.Fields.ContainsKey(field.Name))
								parentType.Fields.Add(field.Name, typeField);
							else
								throw new ConfigurationErrorsException(String.Format("Configuration error: Field {0} already exists in parent edge type {1}", field.Name,  parentType.Name));
								
						}
					}
				}
			}
			catch (Exception ex)
			{
				throw new Exception("Error while trying to get extra fields from DB", ex);
			}
		}
		#endregion
	}
}
