﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Core.Services;
using Edge.Data.Pipeline.Configuration;
using Edge.Data.Pipeline;
using System.Text.RegularExpressions;
using System.Configuration;

namespace Edge.Data.Pipeline.Services
{
	public abstract class PipelineService: Service
	{
		public static class ConfigurationOptionNames
		{
			public const string DeliveryID = "DeliveryID";
			public const string TargetPeriod = "TargetPeriod";
		}

		DateTimeRange? _range = null;
		Delivery _delivery = null;
		Regex[] _trackerPatterns = null;

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
			set
			{
				_delivery = value;
				//_delivery.Saved += new Action<Delivery>((d) => this.WorkflowContext["DeliveryGuid"] = _delivery.Guid.ToString());
			}
		}

		public Guid TargetDeliveryID
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
		
		public Regex[] TrackerPatterns
		{
			get
			{
				if (_trackerPatterns == null)
				{
					ConfigurationElement extension;
					if (!this.Instance.Configuration.ExtendedElements.TryGetValue("Patterns", out extension))
					{
						_trackerPatterns = new Regex[0];
					}
					else
					{
						var regexCollection = (RegexElementCollection)extension;
						_trackerPatterns = new Regex[regexCollection.Count];
						for (int i = 0; i < regexCollection.Count; i++)
							_trackerPatterns[i] = new Regex(regexCollection[i].Pattern, RegexOptions.ExplicitCapture);
					}
				}

				return _trackerPatterns;
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
