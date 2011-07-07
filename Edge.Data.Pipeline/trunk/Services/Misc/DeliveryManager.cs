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

		public DeliveryManager(PipelineService currentService)
		{
			if (currentService == null)s
				throw new ArgumentNullException("currentService");

			this.CurrentService = currentService;
		}

		public abstract void ApplyUniqueness(Delivery delivery);

		public Delivery NewDelivery()
		{
			Delivery delivery = new Delivery(Service.Current.Instance.InstanceID, ((PipelineService)Service.Current).TargetDeliveryID);
			this.ApplyUniqueness(delivery);
			return delivery;
		}

		
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
