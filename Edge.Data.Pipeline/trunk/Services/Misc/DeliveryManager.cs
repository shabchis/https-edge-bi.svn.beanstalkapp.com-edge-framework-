using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Core.Services;

namespace Edge.Data.Pipeline.Services
{
	public abstract class DeliveryManager
	{
		protected PipelineService CurrentService { get; private set; }

		public DeliveryManager()
		{
			if (Service.Current == null || !(Service.Current is PipelineService))
				throw new InvalidOperationException("DeliveryFactory can only be created inside a PipelineService.");

			this.CurrentService = (PipelineService)Service.Current;
		}

		public Delivery NewDelivery()
		{
			Delivery delivery = new Delivery(Service.Current.Instance.InstanceID, ((PipelineService)Service.Current).TargetDeliveryID);
			this.ApplyUniqueness(delivery);
			return delivery;
		}

		internal RollbackOperation HandleConflicts(DeliveryConflictBehavior defaultBehavior, out Delivery newDelivery, bool async)
		{
			DeliveryConflictBehavior behavior = defaultBehavior;
			string configuredBehavior;
			if (Service.Current.Instance.Configuration.Options.TryGetValue("ConflictBehavior", out configuredBehavior))
				behavior = (DeliveryConflictBehavior)Enum.Parse(typeof(DeliveryConflictBehavior), configuredBehavior);

			RollbackOperation operation = null;
			newDelivery = NewDelivery();

			if (behavior != DeliveryConflictBehavior.Ignore)
			{
				Delivery[] conflicting = Delivery.GetSimilars(newDelivery);
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

					if (behavior == DeliveryConflictBehavior.Rollback)
					{
						if (async)
						{
							operation = new RollbackOperation();
							operation.AsyncDelegate = new Action<Delivery[]>(Delivery.Rollback);
							operation.AsyncResult = operation.AsyncDelegate.BeginInvoke(toRollback.ToArray(), null, null);
						}
						else
						{
							Delivery.Rollback(toRollback.ToArray());
						}
					}
					else
					{
						StringBuilder guids = new StringBuilder();
						for (int i = 0; i < conflicting.Length; i++)
						{
							guids.Append(conflicting[i].DeliveryID.ToString("N"));
							if (i < conflicting.Length - 1)
								guids.Append(", ");
						}
						throw new Exception("Conflicting deliveries found: " + guids.ToString());
					}
				}
			}

			return operation;
		}

		public abstract void ApplyUniqueness(Delivery delivery);
	}

	internal class RollbackOperation
	{
		public IAsyncResult AsyncResult;
		public Action<Delivery[]> AsyncDelegate;

		public void Wait()
		{
			this.AsyncResult.AsyncWaitHandle.WaitOne();
			this.AsyncDelegate.EndInvoke(this.AsyncResult);
		}
	}
}
