using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Core.Services;
using Edge.Data.Pipeline.Configuration;
using Edge.Data.Pipeline;
using System.Text.RegularExpressions;
using System.Configuration;
using Edge.Core.Configuration;
using System.Threading;
using System.Data.SqlClient;
using EdgeConfiguration = Edge.Core.Data;
using Edge.Data.Pipeline.Mapping;
using Edge.Core.Utilities;

namespace Edge.Data.Pipeline.Services
{
	public abstract class PipelineService: Service
	{
		#region Core methods
		// ==============================

		protected sealed override ServiceOutcome DoWork()
		{
			ServiceOutcome outcome = DoPipelineWork();
			return outcome;
		}

		protected abstract ServiceOutcome DoPipelineWork();

		protected override void OnEnded(ServiceOutcome outcome)
		{
			// TODO: update delivery history automatically?
		}

		// ==============================
		#endregion

		#region Configuration
		// ==============================

		public static class ConfigurationOptionNames
		{
			public const string DeliveryID = "DeliveryID";
			public const string TimePeriod = "TimePeriod";
			public const string ConflictBehavior = "ConflictBehavior";
		}


		DateTimeRange? _range = null;
		public DateTimeRange TimePeriod
		{
			get
			{
				if (_range == null)
				{
					if (Instance.Configuration.Options.ContainsKey(ConfigurationOptionNames.TimePeriod))
					{
						_range = DateTimeRange.Parse(Instance.Configuration.Options[ConfigurationOptionNames.TimePeriod]);
					}
					else
					{
						_range = DateTimeRange.AllOfYesterday; 
					}

					// enforce limitation
					if (this.TimePeriodLimitation != DateTimeRangeLimitation.None)
					{
						DateTime start = this.TimePeriod.Start.ToDateTime();
						DateTime end = this.TimePeriod.End.ToDateTime();
						switch (this.TimePeriodLimitation)
						{
							case DateTimeRangeLimitation.SameDay:
								if (end.Date != start.Date)
									throw new Exception("The specified range must be within the same day.");
								break;
							case DateTimeRangeLimitation.SameMonth:
								if (end.Month != start.Month || end.Year != start.Year)
									throw new Exception("The specified range must be within the same month.");
								break;
						}
					}
				}

				return _range.Value;
			}
		}

		protected virtual DateTimeRangeLimitation TimePeriodLimitation
		{
			get { return DateTimeRangeLimitation.SameDay; }
		}

		Delivery _delivery = null;
		public Delivery Delivery
		{
			get
			{
				if (_delivery != null)
					return _delivery;

				Guid deliveryID = this.TargetDeliveryID;
				if (deliveryID != Guid.Empty)
					_delivery = DeliveryDB.GetDelivery(deliveryID);

				return _delivery;
			}
			set
			{
				if (this.Delivery != null)
					throw new InvalidOperationException("Cannot apply a delivery because a delivery is already applied.");

				_delivery = value;
			}
		}

		internal Guid TargetDeliveryID
		{
			get
			{
				Guid deliveryID;
				string did;
				if (!Instance.Configuration.Options.TryGetValue(ConfigurationOptionNames.DeliveryID, out did))
					return Guid.Empty;

				if (!Guid.TryParse(did, out deliveryID))
					throw new FormatException(String.Format("'{0}' is not a valid delivery GUID.", did));

				return deliveryID;
			}
		}

		public Delivery NewDelivery()
		{
			var d = new Delivery(this.TargetDeliveryID);
			d.TimePeriodDefinition = this.TimePeriod;

			Log.Write(String.Format("Creating delivery {0}",this.TargetDeliveryID), LogMessageType.Information);
			return d;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="delivery"></param>
		/// <param name="conflictBehavior">Indicates how conflicting deliveries will be handled.</param>
		/// <param name="importManager">The import manager that will be used to handle conflicting deliveries.</param>
		public void HandleConflicts(DeliveryImportManager importManager, DeliveryConflictBehavior defaultConflictBehavior, bool getBehaviorFromConfiguration = true)
		{
			if (this.Delivery.Outputs.Count < 1)
				return;

			// ================================
			// Conflict behavior

			DeliveryConflictBehavior behavior = defaultConflictBehavior;
			if (getBehaviorFromConfiguration)
			{
				string configuredBehavior;
				if (Instance.Configuration.Options.TryGetValue("ConflictBehavior", out configuredBehavior))
					behavior = (DeliveryConflictBehavior)Enum.Parse(typeof(DeliveryConflictBehavior), configuredBehavior);
			}

			
			var processing = new List<DeliveryOutput>();
			var committed = new List<DeliveryOutput>();
			foreach (DeliveryOutput output in this.Delivery.Outputs)
			{
				DeliveryOutput[] conflicts = output.GetConflicting();

				foreach (DeliveryOutput conflict in conflicts)
				{
					if (conflict.ProcessingState != DeliveryOutputProcessingState.Idle)
						processing.Add(conflict);

					if (conflict.Status == DeliveryOutputStatus.Committed)
						committed.Add(conflict);
				}
			}
			if (processing.Count > 0)
				throw new DeliveryConflictException("There are outputs with the same signatures currently being processed:"); // add list of output ids

			if (behavior == DeliveryConflictBehavior.Ignore)
				return;

			if (committed.Count > 0)
				throw new DeliveryConflictException("There are outputs with the same signatures are already committed:"); // add list of output ids

		}

		// ==============================
		#endregion

		#region Mapping
		// ==============================

		MappingConfiguration _mapping = null;
		
		public MappingConfiguration Mappings
		{
			get
			{
				if (_mapping == null)
				{
					ConfigurationElement extension;
					if (!this.Instance.Configuration.Extensions.TryGetValue(MappingConfigurationElement.ExtensionName, out extension))
					{
						AccountElement account = EdgeServicesConfiguration.Current.Accounts.GetAccount(this.Instance.AccountID);
						if (!account.Extensions.TryGetValue(MappingConfigurationElement.ExtensionName, out extension))
							throw new MappingConfigurationException("No mapping configuration found.");
					}
					_mapping = ((MappingConfigurationElement) extension).Load();
				}

				return _mapping;
			}
		}

		// ==============================
		#endregion
	}

	public enum DeliveryConflictBehavior
	{
		Ignore,
		Abort
	}

	public enum DeliveryTicketBehavior
	{
		Ignore,
		Abort
	}


	public enum DeliveryTicketStatus
	{
		ClaimedByOther = 0,
		AlreadyClaimed = 1,
		Claimed = 2
	}

	public enum DateTimeRangeLimitation
	{
		None,
		SameDay,
		SameMonth
	}

}
