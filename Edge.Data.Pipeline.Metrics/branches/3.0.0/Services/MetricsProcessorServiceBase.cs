using System;
using System.Collections.Generic;
using System.Linq;
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
			Accounts	= EdgeObjectConfigLoader.LoadAccounts(_accountId);
			Channels	= EdgeObjectConfigLoader.LoadChannels();
			Measures	= EdgeObjectConfigLoader.LoadMeasures(_accountId);
			EdgeTypes   = EdgeObjectConfigLoader.LoadEdgeTypes(_accountId);
			ExtraFields = EdgeObjectConfigLoader.LoadEdgeFields(_accountId, EdgeTypes);
			EdgeObjectConfigLoader.SetEdgeTypeEdgeFieldRelation(_accountId, EdgeTypes, ExtraFields);

			// Load mapping configuration
			Mappings.ExternalMethods.Add("GetChannel", new Func<dynamic, Channel>(GetChannel));
			Mappings.ExternalMethods.Add("GetCurrentChannel", new Func<Channel>(GetCurrentChannel));
			Mappings.ExternalMethods.Add("GetAccount", new Func<dynamic, Account>(GetAccount));
			Mappings.ExternalMethods.Add("GetCurrentAccount", new Func<Account>(GetCurrentAccount));
			Mappings.ExternalMethods.Add("GetExtraField", new Func<dynamic, EdgeField>(GetExtraField));
			Mappings.ExternalMethods.Add("GetCompositePartField", new Func<dynamic, EdgeField>(GetCompositePartField));
			Mappings.ExternalMethods.Add("GetTargetField", new Func<dynamic, EdgeField>(GetTargetField));
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

		public EdgeField GetExtraField(dynamic fieldName)
		{
			var strFieldName = (string) fieldName;

			var field = ExtraFields.FirstOrDefault(x => x.Name == strFieldName);
			if (field == null)
				throw new MappingException(String.Format("Unknown edge field '{0}'", strFieldName));
			return field;
		}

		public EdgeField GetCompositePartField(dynamic fieldName)
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

		public EdgeField GetTargetField(dynamic fieldName)
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
	}
}
