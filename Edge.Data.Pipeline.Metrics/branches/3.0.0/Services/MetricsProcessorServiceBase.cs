using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using Edge.Core.Configuration;
using Edge.Data.Objects;
using Edge.Data.Pipeline.Mapping;
using Edge.Data.Pipeline.Metrics.Indentity;
using Edge.Data.Pipeline.Metrics.Managers;
using Edge.Data.Pipeline.Metrics.Misc;
using Edge.Data.Pipeline.Objects;
using Edge.Data.Pipeline.Services;
using Eggplant.Entities.Cache;
using Eggplant.Entities.Persistence.SqlServer;
using LogMessageType = Edge.Core.Utilities.LogMessageType;

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
		public List<EdgeField> EdgeFields { get; private set; }
		private Dictionary<string, Dictionary<string, string>> _lookupTable = new Dictionary<string, Dictionary<string, string>>(); 

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

			var store = new SqlPersistenceStore { ConnectionString = AppSettings.GetConnectionString(typeof(MetricsDeliveryManager), Consts.ConnectionStrings.Objects) };
			var cache = new EntityCache();

			// load definitions from DB
			using (var connection = store.ConnectThread() as SqlPersistenceConnection)
			{
				connection.Cache = cache;

				// TODO: temporary load edge types and fields using ConfigLoader, to replace by Cache
				Accounts = EdgeObjectConfigLoader.LoadAccounts(_accountId, connection.DbConnection);
				Channels = EdgeObjectConfigLoader.LoadChannels(connection.DbConnection);
				Measures = EdgeObjectConfigLoader.LoadMeasures(_accountId, connection.DbConnection);

				EdgeTypes = EdgeObjectConfigLoader.LoadEdgeTypes(_accountId, connection.DbConnection);
				EdgeFields = EdgeObjectConfigLoader.LoadEdgeFields(_accountId, EdgeTypes, connection.DbConnection);
				EdgeObjectConfigLoader.SetEdgeTypeEdgeFieldRelation(_accountId, EdgeTypes, EdgeFields, connection.DbConnection);

				//Accounts = Account.Get().ToDictionary(x => x.Name, x => x);
				//Channels = Channel.Get().ToDictionary(x => x.Name, x => x);
				//Measures = Measure.GetInstances(_accountId).ToDictionary(x => x.Name, x => x);
				
				//EdgeTypes = EdgeType.Get(Accounts.Where(x => x.Value.ID == _accountId).Select(x => x.Value).FirstOrDefault()).ToDictionary(x => x.Name, x => x);
				//EdgeFields = EdgeField.Get().ToList();
				//EdgeFields = new List<EdgeField>();
				//foreach (var field in EdgeTypes.Values.SelectMany(type => type.Fields.Where(field => !EdgeFields.Contains(field.Field))))
				//{
				//	EdgeFields.Add(field.Field);
				//}
			}
			// Load mapping configuration
			AddExternalMethods();
			
			Mappings.Compile();
		}

		protected virtual void AddExternalMethods()
		{
			// Load mapping configuration
			Mappings.ExternalMethods.Add("GetChannel", new Func<dynamic, Channel>(GetChannel));
			Mappings.ExternalMethods.Add("GetCurrentChannel", new Func<Channel>(GetCurrentChannel));
			Mappings.ExternalMethods.Add("GetAccount", new Func<dynamic, Account>(GetAccount));
			Mappings.ExternalMethods.Add("GetCurrentAccount", new Func<Account>(GetCurrentAccount));
			Mappings.ExternalMethods.Add("GetEdgeField", new Func<dynamic, EdgeField>(GetEdgeField));
			Mappings.ExternalMethods.Add("GetExtraField", new Func<dynamic, ExtraField>(GetExtraField));
			Mappings.ExternalMethods.Add("GetCompositePartField", new Func<dynamic, CompositePartField>(GetCompositePartField));
			Mappings.ExternalMethods.Add("GetTargetField", new Func<dynamic, TargetField>(GetTargetField));
			Mappings.ExternalMethods.Add("GetEdgeType", new Func<dynamic, EdgeType>(GetEdgeType));
			Mappings.ExternalMethods.Add("GetMeasure", new Func<dynamic, Measure>(GetMeasure));
			Mappings.ExternalMethods.Add("GetObjectByEdgeType", new Func<dynamic, EdgeObject>(GetObjectByEdgeType));
			Mappings.ExternalMethods.Add("GetObjectByEdgeTypeAndEdgeField", new Func<dynamic, dynamic, EdgeObject>(GetObjectByEdgeTypeAndEdgeField));
			Mappings.ExternalMethods.Add("CreatePeriodStart", new Func<dynamic, dynamic, dynamic, DateTime>(CreatePeriodStart));
			Mappings.ExternalMethods.Add("CreatePeriodEnd", new Func<dynamic, dynamic, dynamic, DateTime>(CreatePeriodEnd));
			Mappings.ExternalMethods.Add("GetConfigValue", new Func<dynamic, string>(GetConfigValue));
			Mappings.ExternalMethods.Add("GetDeliveryPeriodStart", new Func<DateTime>(GetDeliveryPeriodStart));
			Mappings.ExternalMethods.Add("GetDeliveryPeriodEnd", new Func<DateTime>(GetDeliveryPeriodEnd));
			Mappings.ExternalMethods.Add("LookupMatch", new Func<dynamic, dynamic, string>(LookupMatch));
		}
		#endregion

		#region Protected Methods
		/// <summary>
		/// Delegate to set EdgeType of object during the mapping (only for EdgeObject or Dictionary of EdgeObjects)
		/// </summary>
		/// <param name="obj"></param>
		protected void SetEdgeType(object obj)
		{
			if (obj is EdgeObject)
			{
				(obj as EdgeObject).EdgeType = GetEdgeType(obj.GetType().Name);
			}
			else if (obj is KeyValuePair<object, object>)
			{
				var pair = (KeyValuePair<object, object>)obj;
				if (pair.Key is EdgeField && pair.Value is EdgeObject)
				{
					(pair.Value as EdgeObject).EdgeType = (pair.Key as EdgeField).FieldEdgeType;
				}
			}
		}
		protected virtual MetricsUnit GetSampleMetrics()
		{
			return null;
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

		public EdgeField GetEdgeField(dynamic fieldName)
		{
			var strFieldName = (string) fieldName;

			var field = EdgeFields.FirstOrDefault(x => x.Name == strFieldName);
			if (field == null)
				throw new MappingException(String.Format("Unknown edge field '{0}'", strFieldName));
			return field;
		}

		public ExtraField GetExtraField(dynamic fieldName)
		{
			return GetEdgeFieldOfType(fieldName, typeof(ExtraField));
		}

		public CompositePartField GetCompositePartField(dynamic fieldName)
		{
			return GetEdgeFieldOfType(fieldName, typeof(CompositePartField));
		}

		public TargetField GetTargetField(dynamic fieldName)
		{
			return GetEdgeFieldOfType(fieldName, typeof(TargetField));
		}

		private EdgeField GetEdgeFieldOfType(dynamic fieldName, Type type)
		{
			var strFieldName = (string)fieldName;

			var field = EdgeFields.FirstOrDefault(x => x.GetType() == type && x.Name == strFieldName);
			if (field == null)
				throw new MappingException(String.Format("Cannot find field '{0}' if type '{1}'", strFieldName, type.ToString()));
			return field;
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

		public string GetConfigValue(dynamic configKey)
		{
			if (!Configuration.Parameters.ContainsKey(configKey.ToString()))
				throw new MappingConfigurationException(String.Format("Missing configuration key '{0}', GetConfigValue() failed.", configKey));

			return Configuration.Parameters[configKey.ToString()].ToString();
		}

		public DateTime GetDeliveryPeriodStart()
		{
			if (Delivery == null)
				throw new MappingException("Delivery is NULL");

			return Delivery.TimePeriodDefinition.Start.BaseDateTime;
		}

		public DateTime GetDeliveryPeriodEnd()
		{
			if (Delivery == null)
				throw new MappingException("Delivery is NULL");

			return Delivery.TimePeriodDefinition.End.BaseDateTime;
		}

		public string LookupMatch(dynamic lookupTableName, dynamic fieldValue)
		{
			// load lookup if not loaded yet (on demand)
			if (!_lookupTable.ContainsKey(lookupTableName))
				LoadLookupTable(lookupTableName);

			// try to find lookup value inside the field
			foreach (var lookupValue in _lookupTable[lookupTableName])
			{
				if (fieldValue.ToString().Contains(lookupValue.Value))
					return lookupValue.Key;
			}

			// warning if no found (extended feature to be varry by behavior: Ignor, Insert new, Warn)
			Log(String.Format("Cannot match any value of Lookup '{0}' in field '{1}'", lookupTableName, fieldValue), LogMessageType.Warning);
			return null;
		}

		private void LoadLookupTable(string lookupTableName)
		{
			_lookupTable.Add(lookupTableName, new Dictionary<string, string>());

			try
			{
				using (var cmd = new SqlCommand("SELECT ValueID,Value FROM SegmentValue sv, Segment s WHERE s.SegmentID = sv.SegmentID and s.Name = @segmentName AND (sv.AccountID = -1 or sv.AccountID = @accountId)", ImportManager.ObjectsConnection))
				{
					cmd.Parameters.AddWithValue("@segmentName", lookupTableName);
					cmd.Parameters.AddWithValue("@accountId", _accountId);

					using (var reader = cmd.ExecuteReader())
					{
						while (reader.Read())
						{
							_lookupTable[lookupTableName].Add(reader["ValueID"].ToString(), reader["Value"].ToString());
						}
					}
				}
			}
			catch (Exception ex)
			{
				throw new Exception(String.Format("Error while trying to load segment '{0}' for account {1} from DB, ex: {3}", lookupTableName, _accountId, ex.Message), ex);
			}
		}

		// ==============================================
		#endregion
	}
}
