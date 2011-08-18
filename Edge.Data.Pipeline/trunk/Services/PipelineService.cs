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
			public const string ConflictBehavior = "ConflictBehavior";
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
			return new Delivery(this.Instance.InstanceID, this.TargetDeliveryID);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="delivery"></param>
		/// <param name="conflictBehavior">Indicates how conflicting deliveries will be handled.</param>
		/// <param name="importManager">The import manager that will be used to handle conflicting deliveries.</param>
		public DeliveryRollbackOperation HandleConflicts(DeliveryImportManager importManager, DeliveryConflictBehavior defaultConflictBehavior, bool getBehaviorFromConfiguration = true)
		{
			DeliveryConflictBehavior behavior = defaultConflictBehavior;
			if (getBehaviorFromConfiguration)
			{
				string configuredBehavior;
				if (Instance.Configuration.Options.TryGetValue("ConflictBehavior", out configuredBehavior))
					behavior = (DeliveryConflictBehavior)Enum.Parse(typeof(DeliveryConflictBehavior), configuredBehavior);
			}

			if (behavior == DeliveryConflictBehavior.Ignore)
				return null;

			if (this.Delivery.Signature == null)
				throw new InvalidOperationException("Cannot handle conflicts before a valid signature is given to the delivery.");

			Delivery[] conflicting = this.Delivery.GetConflicting();
			Delivery[] toCheck = new Delivery[conflicting.Length + 1];
			toCheck[0] = this.Delivery;
			conflicting.CopyTo(toCheck, 1);

			// Check whether the last commit was not rolled back for each conflicting delivery
			bool conflictsFound = false;
			List<Delivery> inProcess = new List<Delivery>();
			List<Delivery> toRollback = new List<Delivery>();

			foreach (Delivery d in toCheck)
			{
				int rollbackIndex = -1;
				int commitIndex = -1;
				int abortedIndex = -1;
				int otherIndex = -1;

				for (int i = 0; i < d.History.Count; i++)
				{
					if (d.History[i].Operation == DeliveryOperation.Committed)
						commitIndex = i;
					else if (d.History[i].Operation == DeliveryOperation.RolledBack)
						rollbackIndex = i;
					else if (d.History[i].Operation == DeliveryOperation.Aborted)
						abortedIndex = i;
					else
						otherIndex = i;
				}

				// Conflicts means either an unrolled back commit or the last operation not being an abort


				if (commitIndex > rollbackIndex)
				{
					toRollback.Add(d);
					conflictsFound = true;
				}

				if (otherIndex > abortedIndex && otherIndex > commitIndex)
				{
					inProcess.Add(d);
					conflictsFound = true;
				}
			}

			DeliveryRollbackOperation operation = null;
			if (conflictsFound)
			{
				if (behavior == DeliveryConflictBehavior.Rollback && inProcess.Count == 0)
				{
					operation = new DeliveryRollbackOperation();
					operation.AsyncDelegate = new Action<Delivery[]>(importManager.Rollback);
					operation.AsyncResult = operation.AsyncDelegate.BeginInvoke(toRollback.ToArray(), null, null);	
				}
				else
				{
					var guidOutputList = new List<Delivery>(inProcess);
					guidOutputList.AddRange(toRollback);

					StringBuilder guids = new StringBuilder();
					for (int i = 0; i < guidOutputList.Count; i++)
					{
						guids.Append(guidOutputList[i].DeliveryID.ToString("N"));
						if (i < guidOutputList.Count - 1)
							guids.Append(", ");
					}

					this.Delivery.History.Add(DeliveryOperation.Aborted, this.Instance.InstanceID);
					this.Delivery.Save();

					throw new Exception("Conflicting deliveries found: " + guids.ToString());
				}
			}

			return operation;
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
					ConfigurationElement extension;
					if (!this.Instance.Configuration.Extensions.TryGetValue(AutoSegmentDefinitionCollection.ExtensionName, out extension))
					{
						AccountElement account = EdgeServicesConfiguration.Current.Accounts.GetAccount(this.Instance.AccountID);
						if (!account.Extensions.TryGetValue(AutoSegmentDefinitionCollection.ExtensionName, out extension))
							throw new ConfigurationException("No AutoSegments configuration found.");
					}
					_autoSegments = new AutoSegmentationUtility(extension as AutoSegmentDefinitionCollection);
				}

				return _autoSegments;
			}
		}

		// ==============================
		#endregion
	}

	public enum DeliveryConflictBehavior
	{
		Ignore,
		Abort,
		Rollback
	}

	public class DeliveryRollbackOperation
	{
		internal IAsyncResult AsyncResult;
		internal Action<Delivery[]> AsyncDelegate;

		public void Wait()
		{
			this.AsyncResult.AsyncWaitHandle.WaitOne();
			this.AsyncDelegate.EndInvoke(this.AsyncResult);
		}
	}

}
