using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Core.Data;
using Edge.Data.Objects;
using Edge.Data.Pipeline;
using Edge.Data.Pipeline.Services;
using Edge.Data.Pipeline.Mapping;
using Edge.Data.Pipeline.Common.Importing;
using System.IO;
using System.Data.SqlClient;
using Edge.Core.Configuration;
using Edge.Core.Services;
using Edge.Data.Pipeline.Metrics.AdMetrics;

namespace Edge.Data.Pipeline.Metrics.Services
{
	public abstract class MetricsProcessorServiceBase : PipelineService
	{
		public Dictionary<string, Account> Accounts {get; private set;}
		public Dictionary<string, Channel> Channels {get; private set;}
        public Dictionary<string, CurrencyRate> CurrencyRates { get; private set; }
		public MetricsImportManager ImportManager { get; protected set; }

		protected override void OnInit()
		{
			Accounts = GetAccountsFromDB(Instance.AccountID);
			Channels = GetChannelsFromDB();
            CurrencyRates = CurrencyRate.GetCurrencyRates(this.Delivery.TimePeriodStart);
			
			// Load mapping configuration
			// ------------------------------------------
			this.Mappings.ExternalMethods.Add("GetChannel", new Func<dynamic, Channel>(GetChannel));
			this.Mappings.ExternalMethods.Add("GetCurrentChannel", new Func<Channel>(GetCurrentChannel));
			this.Mappings.ExternalMethods.Add("GetAccount", new Func<dynamic, Account>(GetAccount));
			this.Mappings.ExternalMethods.Add("GetCurrentAccount", new Func<Account>(GetCurrentAccount));
			this.Mappings.ExternalMethods.Add("GetSegment", new Func<dynamic, Segment>(GetSegment));
            this.Mappings.ExternalMethods.Add("GetMeasure", new Func<dynamic, Measure>(GetMeasure));
            this.Mappings.ExternalMethods.Add("ConvertToUSD", new Func<dynamic,dynamic, double>(ConvertToUSD));
			this.Mappings.ExternalMethods.Add("CreatePeriodStart", new Func<dynamic, dynamic, dynamic, DateTime>(CreatePeriodStart));
			this.Mappings.ExternalMethods.Add("CreatePeriodEnd", new Func<dynamic, dynamic, dynamic, DateTime>(CreatePeriodEnd));

			OnInitMappings();

			this.Mappings.Compile();
		}

		protected virtual void OnInitMappings()
		{
		}
	

		#region Scriptable methods
		// ==============================================

        public Double ConvertToUSD (dynamic rateCode,dynamic sourceValue)
        {
            if (((string)rateCode).ToUpper().Equals("USD")) 
                return 1;
            
            var code = ((string)rateCode).ToUpper();
            CurrencyRate rate;
            if (!CurrencyRates.TryGetValue(code, out rate))
                throw new MappingException(String.Format("Currncy code '{0}' could not be found in DB.", rateCode));
            return rate.RateValue * sourceValue;
        }

        public Account GetAccount(dynamic name)
		{
			var n = (string)name;
			Account a;
			if (!Accounts.TryGetValue(n, out a))
				throw new MappingException(String.Format("No account named '{0}' could be found, or it cannot be used from within account #{1}.", n, Instance.AccountID));
			return a;
		}

		public Account GetCurrentAccount()
		{
			return new Account() { ID = this.Instance.AccountID};
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
			return this.Delivery.Channel;
		}

		public Segment GetSegment(dynamic name)
		{
			var n = (string)name;
			Segment s;
			if (!ImportManager.SegmentTypes.TryGetValue(n, out s))
				throw new MappingException(String.Format("No segment named '{0}' could be found.", n));
			return s;
		}

		public Measure GetMeasure(dynamic name)
		{
			var n = (string)name;
			Measure m;
			if (!ImportManager.Measures.TryGetValue(n, out m))
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

			DateTime period;
			period = new DateTimeSpecification()
			{
				Alignment = align,
				BaseDateTime = baseDateTime
			}
				.ToDateTime();

			return period;
		}

		// ==============================================
		#endregion

		private Dictionary<string, Account> GetAccountsFromDB(int currentAccountId)
		{

			Dictionary<string, Account> accounts = new Dictionary<string, Account>();
			SqlConnection connection;
			connection = new SqlConnection(AppSettings.GetConnectionString(typeof(AdMetricsImportManager), "StagingDatabase"));
			try
			{
				using (connection)
				{
					SqlCommand cmd = DataManager.CreateCommand(@"GetAccountFamily_ById(@CurrentAccountId:int)", System.Data.CommandType.StoredProcedure);
					cmd.Connection = connection;
					connection.Open();
					cmd.Parameters["@CurrentAccountId"].Value = currentAccountId;

					using (SqlDataReader reader = cmd.ExecuteReader())
					{
						while (reader.Read())
						{
							Account account = new Account()
							{
								ID = Convert.ToInt32(reader[1]),
								Name = Convert.ToString(reader[0])
							};
							accounts.Add(account.Name, account);
						}
					}
				}
			}
			catch (Exception ex)
			{
				throw new Exception("Error while trying to get accounts from DB", ex);
			}
			return accounts;
			
		}
		private Dictionary<string, Channel> GetChannelsFromDB()
		{
			Dictionary<string, Channel> channels = new Dictionary<string, Channel>(StringComparer.CurrentCultureIgnoreCase);
			SqlConnection connection;
			connection = new SqlConnection(AppSettings.GetConnectionString(typeof(AdMetricsImportManager), "StagingDatabase"));
			try
			{
				using (connection)
				{
					SqlCommand cmd = DataManager.CreateCommand(@"GetChannels()", System.Data.CommandType.StoredProcedure);
					cmd.Connection = connection;
					connection.Open();
					using (SqlDataReader reader = cmd.ExecuteReader())
					{
						while (reader.Read())
						{
							Channel channel = new Channel()
							{
								ID = Convert.ToInt16(reader[0]),
								Name = Convert.ToString(reader[1])
							};
							channels.Add(channel.Name, channel);
						}
					}
				}
				//var a=channels.Where(pp=>pp.Key.ToLower()=="fff".tol
			}
			catch (Exception ex)
			{
				throw new Exception("Error while trying to get Channels from DB", ex);
			}
			return channels;
		}

		
	}

	

	
}
