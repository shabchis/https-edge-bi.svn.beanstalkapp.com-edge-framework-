﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using Edge.Core.Configuration;
using Edge.Core.Utilities;
using Edge.Data.Objects;
using Edge.Data.Pipeline.Mapping;
using Edge.Data.Pipeline.Metrics.Base;
using Edge.Data.Pipeline.Services;

namespace Edge.Data.Pipeline.Metrics.Services
{
	public abstract class MetricsProcessorServiceBase : PipelineService
	{
		#region Properties
		public Dictionary<string, Account> Accounts { get; private set; }
		public Dictionary<string, Channel> Channels { get; private set; }
		public Dictionary<string, Measure> Measures { get; private set; }
		public Dictionary<string, ExtraField> ExtraFields { get; private set; }

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

			LoadAccounts();
			LoadChannels();
			LoadMeasures();
			// TODO - load extra fields definitions from DB
			//LoadExtraFields();

			// Load mapping configuration
			// ------------------------------------------
			Mappings.ExternalMethods.Add("GetChannel", new Func<dynamic, Channel>(GetChannel));
			Mappings.ExternalMethods.Add("GetCurrentChannel", new Func<Channel>(GetCurrentChannel));
			Mappings.ExternalMethods.Add("GetAccount", new Func<dynamic, Account>(GetAccount));
			Mappings.ExternalMethods.Add("GetCurrentAccount", new Func<Account>(GetCurrentAccount));
			//Mappings.ExternalMethods.Add("GetSegment", new Func<dynamic, Segment>(GetSegment));
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

		public ExtraField GetExtraField(dynamic name)
		{
			var n = (string)name;
			ExtraField field;
			if (!ExtraFields.TryGetValue(n, out field))
				throw new MappingException(String.Format("No extra field named '{0}' could be found.", n));
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
					var cmd = SqlUtility.CreateCommand("Account_GetByAccountId", CommandType.StoredProcedure);
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
					var cmd = SqlUtility.CreateCommand("Channel_GetList", CommandType.StoredProcedure);
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

		/// <summary>
		/// Load meatures from DB by account
		/// </summary>
		private void LoadMeasures()
		{
			Measures = new Dictionary<string, Measure>();
			try
			{
				using (var connection = new SqlConnection(AppSettings.GetConnectionString(typeof(MetricsProcessorServiceBase), Consts.ConnectionStrings.Objects)))
				{
					var cmd = SqlUtility.CreateCommand("Measure_GetByAccountId", CommandType.StoredProcedure);
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

		/// <summary>
		/// Load extra fields definition by account
		/// </summary>
		private void LoadExtraFields()
		{
 			ExtraFields = new Dictionary<string, ExtraField>();
			try
			{
				using (var connection = new SqlConnection(AppSettings.GetConnectionString(typeof(MetricsProcessorServiceBase), Consts.ConnectionStrings.Objects)))
				{
					var cmd = SqlUtility.CreateCommand("ExtraFields_GetByAccountId", CommandType.StoredProcedure);
					cmd.Parameters.AddWithValue("@accountID", _accountId);
					cmd.Connection = connection;
					connection.Open();

					using (var reader = cmd.ExecuteReader())
					{
						while (reader.Read())
						{
							var field = new ExtraField
							{
								// fill data from DB row
							};
							ExtraFields.Add(field.Name, field);
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
