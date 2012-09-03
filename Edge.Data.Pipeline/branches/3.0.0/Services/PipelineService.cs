using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Core.Services;
using Edge.Data.Pipeline;
using System.Text.RegularExpressions;
using System.Configuration;
using Edge.Core.Configuration;
using System.Threading;
using System.Data.SqlClient;
using Edge.Data.Pipeline.Mapping;
using Edge.Core.Utilities;

namespace Edge.Data.Pipeline.Services
{
	public abstract class PipelineService : Service
	{
		#region Core methods
		// ==============================

		protected sealed override ServiceOutcome DoWork()
		{
			ServiceOutcome outcome = DoPipelineWork();
			return outcome;
		}

		protected abstract ServiceOutcome DoPipelineWork();

		// ==============================
		#endregion

		#region Configuration
		// ==============================

		public new PipelineServiceConfiguration Configuration
		{
			get { return (PipelineServiceConfiguration)base.Configuration; }
		}

		/*
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
		*/

		Delivery _delivery = null;
		public Delivery Delivery
		{
			get
			{
				if (_delivery != null)
					return _delivery;

				if (this.Configuration.DeliveryID != null)
					_delivery = DeliveryDB.GetDelivery(this.Configuration.DeliveryID.Value);

				return _delivery;
			}
			set
			{
				if (this.Delivery != null)
					throw new InvalidOperationException("Cannot apply a delivery because a delivery is already applied.");

				_delivery = value;
			}
		}

		public Delivery NewDelivery()
		{
			var d = new Delivery(this.Configuration.DeliveryID.Value);
			d.TimePeriodDefinition = this.Configuration.TimePeriod.Value;

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

			ServiceInstance parent = this.AsServiceInstance();
			while (parent.ParentInstance != null)			
				parent = parent.ParentInstance;


			foreach (DeliveryOutput output in this.Delivery.Outputs)
			{
				if (!output.PipelineInstanceID.HasValue)
					output.PipelineInstanceID = parent.InstanceID;
			}

			this.Delivery.Save();

			// ================================
			// Conflict behavior

			DeliveryConflictBehavior behavior = this.Configuration.ConflictBehavior != null ?
				this.Configuration.ConflictBehavior.Value :
				defaultConflictBehavior;

			var processing = new List<DeliveryOutput>();
			var committed = new List<DeliveryOutput>();
			foreach (DeliveryOutput output in this.Delivery.Outputs)
			{
				DeliveryOutput[] conflicts = output.GetConflicting();

				foreach (DeliveryOutput conflict in conflicts)
				{
					if (conflict.PipelineInstanceIsRunning)
						processing.Add(conflict);

					if (conflict.Status == DeliveryOutputStatus.Committed || conflict.Status==DeliveryOutputStatus.Staged)
						committed.Add(conflict);
				}
			}
			if (processing.Count > 0)
			{
				foreach (var output in Delivery.Outputs)
					output.Status = DeliveryOutputStatus.Canceled;

				this.Delivery.Save();
				throw new DeliveryConflictException("There are outputs with the same signatures currently being processed:") { ConflictingOutputs = processing.ToArray() }; // add list of output ids
			}

			if (behavior == DeliveryConflictBehavior.Ignore)
				return;

			if (committed.Count > 0)
			{
				foreach (var output in Delivery.Outputs)
					output.Status = DeliveryOutputStatus.Canceled;


				this.Delivery.Save();
				throw new DeliveryConflictException("There are outputs with the same signatures are already committed\\staged:") { ConflictingOutputs = committed.ToArray() }; // add list of output ids
			}

		}

		// ==============================
		#endregion

		#region Mapping
		// ==============================

		/*
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
							extension = null;
					}
					_mapping = new MappingConfiguration();
					_mapping.Usings.Add("System.{0}, mscorlib");
					_mapping.Usings.Add("Edge.Data.Objects.{0}, Edge.Data.Pipeline");

					if (extension != null)
						((MappingConfigurationElement)extension).LoadInto(_mapping);
				}

				return _mapping;
			}
		}
		*/

		// ==============================
		#endregion
	}


	[Serializable]
	public class PipelineServiceConfiguration : ServiceConfiguration
	{
		Guid? _deliveryID;
		public Guid? DeliveryID { get { return _deliveryID; } set { EnsureUnlocked(); _deliveryID = value; } }

		DateTimeRange? _timePeriod;
		public DateTimeRange? TimePeriod { get { return _timePeriod; } set { EnsureUnlocked(); _timePeriod = value; } }

		DeliveryConflictBehavior? _conflictBehavior;
		public DeliveryConflictBehavior? ConflictBehavior { get { return _conflictBehavior; } set { EnsureUnlocked(); _conflictBehavior = value; } }
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
