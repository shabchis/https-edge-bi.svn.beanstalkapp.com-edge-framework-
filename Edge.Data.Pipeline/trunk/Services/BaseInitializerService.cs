using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Pipeline.Services
{
	public abstract class BaseInitializerService: PipelineService
	{
		public abstract DeliveryManager GetDeliveryManager();
		public abstract void InitializeDelivery();

		protected sealed override Core.Services.ServiceOutcome DoPipelineWork()
		{
			Delivery newDelivery;
			DeliveryManager deliveryManager = GetDeliveryManager();

			deliveryManager.HandleConflicts(DeliveryConflictBehavior.Abort, out newDelivery, false);
			this.Delivery = newDelivery;

			InitializeDelivery();

			return Core.Services.ServiceOutcome.Success;
		}
	}
}
