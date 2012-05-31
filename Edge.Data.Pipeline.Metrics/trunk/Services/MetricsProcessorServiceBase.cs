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

namespace Edge.Data.Pipeline.Metrics.Services
{
	public abstract class MetricsProcessorServiceBase : PipelineService
	{
		public Dictionary<string, Account> Accounts {get; private set;}
		public Dictionary<string, Channel> Channels {get; private set;}
		public MetricsImportManager ImportManager { get; protected set; }

		protected override void OnInit()
		{			
			// Load mapping configuration
			// ------------------------------------------

			this.Mappings.ExternalMethods.Add("GetChannel", new Func<string, Channel>(GetChannel));
			this.Mappings.ExternalMethods.Add("GetCurrentChannel", new Func<Channel>(GetCurrentChannel));
			this.Mappings.ExternalMethods.Add("GetAccount", new Func<string, Account>(GetAccount));
			this.Mappings.ExternalMethods.Add("GetCurrentAccount", new Func<Account>(GetCurrentAccount));
			this.Mappings.ExternalMethods.Add("GetSegment", new Func<string, Segment>(GetSegment));
			this.Mappings.ExternalMethods.Add("GetMeasure", new Func<string, Measure>(GetMeasure));
			this.Mappings.ExternalMethods.Add("CreatePeriodStart", new Func<string, string, string, DateTime>(CreatePeriodStart));
			this.Mappings.ExternalMethods.Add("CreatePeriodEnd", new Func<string, string, string, DateTime>(CreatePeriodEnd));
			this.Mappings.Compile();
		}		

		#region Scriptable methods
		// ==============================================

		public Account GetAccount(string name)
		{
			Account a;
			if (!Accounts.TryGetValue(name, out a))
				throw new MappingException(String.Format("No account named '{0}' could be found, or it cannot be used from within account #{1}.", name, Instance.AccountID));
			return a;
		}

		public Account GetCurrentAccount()
		{
			return new Account() { ID = this.Instance.AccountID};
		}

		public Channel GetChannel(string name)
		{
			Channel c;
			if (!Channels.TryGetValue(name, out c))
				throw new MappingException(String.Format("No channel named '{0}' could be found.", name));
			return c;
		}

		public Channel GetCurrentChannel()
		{
			return this.Delivery.Channel;
		}

		public Segment GetSegment(string name)
		{
			Segment s;
			if (!ImportManager.SegmentTypes.TryGetValue(name, out s))
				throw new MappingException(String.Format("No segment named '{0}' could be found.", name));
			return s;
		}

		public Measure GetMeasure(string name)
		{
			Measure m;
			if (!ImportManager.Measures.TryGetValue(name, out m))
				throw new MappingException(String.Format("No measure named '{0}' could be found. Make sure you specified the base measure name, not the display name.", name));
			return m;
		}

		public DateTime CreatePeriodStart(string year, string month, string day)
		{
			return CreatePeriod(DateTimeSpecificationAlignment.Start, year, month, day);
		}

		public DateTime CreatePeriodEnd(string year, string month, string day)
		{
			return CreatePeriod(DateTimeSpecificationAlignment.End, year, month, day);
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
	}

	

	
}
