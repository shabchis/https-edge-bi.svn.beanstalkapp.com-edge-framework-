using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Core.Services;
using Edge.Data.Pipeline.Configuration;
using Edge.Data.Pipeline;
using Edge.Data.Pipeline.Deliveries;

namespace Edge.Data.Pipeline.Services
{
	public abstract class PipelineService: Service
	{
		DateTimeRange? _range = null;
		Delivery _delivery = null;

		public DateTimeRange TargetPeriod
		{
			get
			{
				if (_range == null)
					_range = DateTimeRange.Parse(Instance.Configuration.Options["TargetPeriod"]);
				return _range.Value;
			}
		}

		/*
		public ReportConfigurationElement ReportSettings
		{
			get { return Instance.Configuration.ExtendedElements["ReportSettings"] as ReportConfigurationElement; }
		}
		*/

		public Delivery Delivery
		{
			get
			{
				if (_delivery != null)
					return _delivery;

				int deliveryID;
				string did;
				if (!this.WorkflowContext.TryGetValue("DeliveryID", out did))
					if (!Instance.Configuration.Options.TryGetValue("DeliveryID", out did))
						return null;

				if (!int.TryParse(did, out deliveryID))
					throw new Exception(String.Format("'{0}' is not a valid delivery ID.", did));

				//_delivery = DeliveryDB.Get(deliveryID);
				this.WorkflowContext["DeliveryID"] = did;
				return _delivery;
			}
			set
			{
				_delivery = value;
				_delivery.Saved += new Action<Delivery>((d) => this.WorkflowContext["DeliveryID"] = _delivery.DeliveryID.ToString());
			}
		}

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
	}
}
