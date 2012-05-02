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
		public MetricsImportManager ImportManager { get; private set; }
		public ReaderAdapter ReaderAdapter { get; private set; }

		protected sealed override Core.Services.ServiceOutcome DoPipelineWork()
		{
			// Setup/defaults/configuration/etc.
			// ------------------------------------------

			string fileName;
			if (!this.Instance.Configuration.Options.TryGetValue(Const.DeliveryServiceConfigurationOptions.DeliveryFileName, out fileName))
				throw new ConfigurationException(String.Format("{0} is missing in the service configuration options.", Const.DeliveryServiceConfigurationOptions.DeliveryFileName));

			DeliveryFile file = this.Delivery.Files[fileName];
			if (file == null)
				throw new Exception(String.Format("Could not find delivery file '{0}' in the delivery.", fileName));

			FileCompression compression;
			string compressionOption;
			if (this.Instance.Configuration.Options.TryGetValue(Const.DeliveryServiceConfigurationOptions.Compression, out compressionOption))
			{
				if (!Enum.TryParse<FileCompression>(compressionOption, out compression))
					throw new ConfigurationException(String.Format("Invalid compression type '{0}'.", compressionOption));
			}
			else
				compression = FileCompression.None;

			string checksumThreshold = Instance.Configuration.Options[Consts.ConfigurationOptions.ChecksumTheshold];
			var importManagerOptions = new MetricsImportManagerOptions()
			{
				SqlPrepareCommand = Instance.Configuration.Options[Consts.AppSettings.SqlPrepareCommand],
				SqlCommitCommand = Instance.Configuration.Options[Consts.AppSettings.SqlCommitCommand],
				SqlRollbackCommand = Instance.Configuration.Options[Consts.AppSettings.SqlRollbackCommand],
				ChecksumThreshold = checksumThreshold == null ? 0.01 : double.Parse(checksumThreshold)
			};

			// Create format processor from configuration
			string adapterTypeName = Instance.Configuration.GetOption(Consts.ConfigurationOptions.ReaderAdapterType);
			Type readerAdapterType = Type.GetType(adapterTypeName, true);
			this.ReaderAdapter = (ReaderAdapter)Activator.CreateInstance(readerAdapterType);

			// Load mapping configuration
			// ------------------------------------------

			this.Mappings.ExternalMethods.Add("GetChannel", new Func<string, Channel>(GetChannel));
			this.Mappings.ExternalMethods.Add("GetAccount", new Func<string, Account>(GetAccount));
			this.Mappings.ExternalMethods.Add("GetSegment", new Func<string, Segment>(GetSegment));
			this.Mappings.ExternalMethods.Add("GetMeasure", new Func<string, Measure>(GetMeasure));
			this.Mappings.ExternalMethods.Add("CreatePeriodStart", new Func<string, string, string, DateTime>(CreatePeriodStart));
			this.Mappings.ExternalMethods.Add("CreatePeriodEnd", new Func<string, string, string, DateTime>(CreatePeriodEnd));

			this.Mappings.OnFieldRequired = this.ReaderAdapter.GetField;


			LoadConfiguration();

			// ------------------------------------------

			using (var stream = file.OpenContents(compression: compression))
			{
				using (this.ReaderAdapter)
				{
					this.ReaderAdapter.Init(stream, Instance.Configuration);

					using (this.ImportManager = CreateImportManager(Instance.InstanceID, importManagerOptions))
					{
						while (this.ReaderAdapter.Reader.Read())
							OnRead();

						this.ImportManager.EndImport();
					}
				}
			}

			return Core.Services.ServiceOutcome.Success;
		}

		protected abstract MetricsImportManager CreateImportManager(long serviceInstanceID, MetricsImportManagerOptions options);
		protected abstract void OnRead();
		protected virtual void LoadConfiguration() { }

		#region Scriptable methods
		// ==============================================

		public Account GetAccount(string name)
		{
			Account a;
			if (!Accounts.TryGetValue(name, out a))
				throw new MappingException(String.Format("No account named '{0}' could be found, or it cannot be used from within account #{1}.", name, Instance.AccountID));
			return a;
		}

		public Channel GetChannel(string name)
		{
			Channel c;
			if (!Channels.TryGetValue(name, out c))
				throw new MappingException(String.Format("No channel named '{0}' could be found.", name));
			return c;
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
