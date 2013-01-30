using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using Edge.Core.Configuration;
using Edge.Core.Utilities;
using Edge.Data.Objects;
using Edge.Data.Pipeline.Mapping;
using Edge.Data.Pipeline.Metrics.Managers;
using Edge.Data.Pipeline.Metrics.Misc;
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
			LoadExtraFields();

			// Load mapping configuration
			Mappings.ExternalMethods.Add("GetChannel", new Func<dynamic, Channel>(GetChannel));
			Mappings.ExternalMethods.Add("GetCurrentChannel", new Func<Channel>(GetCurrentChannel));
			Mappings.ExternalMethods.Add("GetAccount", new Func<dynamic, Account>(GetAccount));
			Mappings.ExternalMethods.Add("GetCurrentAccount", new Func<Account>(GetCurrentAccount));
			Mappings.ExternalMethods.Add("GetExtraField", new Func<dynamic, ExtraField>(GetExtraField));
			Mappings.ExternalMethods.Add("GetCompositePartField", new Func<dynamic, CompositePartField>(GetCompositePartField));
			Mappings.ExternalMethods.Add("GetEdgeType", new Func<dynamic, EdgeType>(GetEdgeType));
			Mappings.ExternalMethods.Add("GetMeasure", new Func<dynamic, Measure>(GetMeasure));
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

		public ExtraField GetExtraField(dynamic name)
		{
			var n = (string)name;
			var field = ExtraFields.FirstOrDefault(x => x.Name == n);
			if (field == null)
				throw new MappingException(String.Format("No edge field named '{0}' could be found.", n));
			return field;
		}

		public CompositePartField GetCompositePartField(dynamic name)
		{
			var n = (string)name;
			var field = ExtraFields.FirstOrDefault(x => x.Name == n);
			if (field == null)
				throw new MappingException(String.Format("No edge field named '{0}' could be found.", n));
			return new CompositePartField(field);
		}

		public Measure GetMeasure(dynamic name)
		{
			var n = (string)name;
			Measure m;
			if (!Measures.TryGetValue(n, out m))
				throw new MappingException(String.Format("No measure named '{0}' could be found. Make sure you specified the base measure name, not the display name.", n));
			return m;
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

		private void LoadExtraFields()
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
									ColumnIndex = int.Parse(reader["ColumnIndex"].ToString()),
									ColumnType = reader["ColumnType"].ToString(),
									FieldClrType = Type.GetType(reader["FieldClrType"].ToString()),
									FieldEdgeType = EdgeTypes.Values.FirstOrDefault(x => x.TypeID == int.Parse(reader["EdgeTypeID"].ToString())),
									ParentEdgeType = EdgeTypes.Values.FirstOrDefault(x => x.TypeID == int.Parse(reader["ParentEdgeTypeID"].ToString()))
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
		#endregion
	}
}
