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

namespace Edge.Data.Pipeline.Services
{
	public abstract class PipelineService: Service
	{
		//................................................
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
		//................................................

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

				Guid deliveryID = this.DeliveryID;
				if (deliveryID != Guid.Empty)
					_delivery = DeliveryDB.Get(deliveryID);

				return _delivery;
			}
			set
			{
				_delivery = value;
				//_delivery.Saved += new Action<Delivery>((d) => this.WorkflowContext["DeliveryGuid"] = _delivery.Guid.ToString());
			}
		}

		public Guid DeliveryID
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

		protected void HandleConflictingDeliveries()
		{
			throw new NotImplementedException();
			/*
			Delivery[] conflicting = this.NewDeliveryWithMinimalValues().GetConflicting();
			if (conflicting.Length > 0)
			{
				if (Instance.Configuration.Options["Lidros"])
				{
					foreach (Delivery delivery in conflicting)
						delivery.Rollback(guid);
				}
				else
					throw new Exception("Data already exists");
			}
			*/
		}

		/// <summary>
		/// This method must be implemented in order to use HandleConflictingDeliveries.
		/// It must return a new Delivery object with settings/parameters that should be considered
		/// unique, i.e. if another delivery exists with the same settings/parameters, its data will be rolled back.
		/// </summary>
		/// <returns></returns>
		protected virtual Delivery NewDeliveryWithMinimalValues()
		{
			throw new NotImplementedException();
		}



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

	}
}
