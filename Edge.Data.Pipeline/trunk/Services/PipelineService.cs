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
using Edge.Data.Pipeline.Importing;

namespace Edge.Data.Pipeline.Services
{
	public abstract class PipelineService: Service
	{
		#region Core methods
		// ==============================

		protected sealed override void OnInit()
		{
			// TODO: check for required configuration options
		}

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
			public const string TargetPeriod = "TargetPeriod";
		}


		DateTimeRange? _range = null;
		public DateTimeRange TargetPeriod
		{
			get
			{
				if (_range == null)
				{
					if (Instance.Configuration.Options.ContainsKey(ConfigurationOptionNames.TargetPeriod))
					{
						_range = DateTimeRange.Parse(Instance.Configuration.Options[ConfigurationOptionNames.TargetPeriod]);
					}
					else
					{
						_range = DateTimeRange.AllOfYesterday; 
					}
				}

				return _range.Value;
			}
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
					_delivery = DeliveryDB.Get(deliveryID);

				return _delivery;
			}
			private set
			{
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
			return new Delivery(this.Instance.InstanceID, this.TargetDeliveryID);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="delivery"></param>
		/// <param name="conflictBehavior">Indicates how conflicting deliveries will be handled.</param>
		/// <param name="importManager">The import manager that will be used to handle conflicting deliveries.</param>
		public void ApplyDelivery(Delivery delivery, DeliveryImportManager importManager = null, DeliveryConflictBehavior conflictBehavior = DeliveryConflictBehavior.Default)
		{
			if (this.Delivery != null)
				throw new InvalidOperationException("Cannot apply a delivery because a delivery is already applied.");

			Delivery[] conflicting = delivery.GetConflicting();
			if (conflicting.Length > 0)
			{
				// Check whether the last commit was not rolled back for each conflicting delivery
				List<Delivery> toRollback = new List<Delivery>();
				foreach (Delivery d in conflicting)
				{
					int rollbackIndex = -1;
					int commitIndex = -1;
					for (int i = 0; i < d.History.Count; i++)
					{
						if (d.History[i].Operation == DeliveryOperation.Comitted)
							commitIndex = i;
						else if (d.History[i].Operation == DeliveryOperation.RolledBack)
							rollbackIndex = i;
					}

					if (commitIndex > rollbackIndex)
						toRollback.Add(d);
				}
			}
		}

		// ==============================
		#endregion

		#region Auto segments
		// ==============================

		AutoSegmentationUtility _autoSegments = null;
		
		public AutoSegmentationUtility AutoSegments
		{
			get
			{
				if (_autoSegments == null)
				{
					AccountElement account = EdgeServicesConfiguration.Current.Accounts.GetAccount(this.Instance.AccountID);
					_autoSegments = new AutoSegmentationUtility(account.Extensions[AutoSegmentDefinitionCollection.ExtensionName] as AutoSegmentDefinitionCollection);
				}

				return _autoSegments;
			}
		}

		// ==============================
		#endregion
	}

	public enum DeliveryConflictBehavior
	{
		Default,
		Ignore,
		Abort,
		Rollback
	}

}
