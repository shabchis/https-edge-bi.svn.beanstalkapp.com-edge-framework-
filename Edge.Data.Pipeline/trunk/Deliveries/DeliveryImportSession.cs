using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Pipeline.Importing
{
	public abstract class DeliveryImportSession<T>
	{
		Delivery _delivery;

		public DeliveryImportSession(Delivery delivery)
		{
			if (delivery.DeliveryID == Guid.Empty)
			{
				//throw new InvalidOperationException("An import session can only be created for a delivery that has been saved.");
			}

			_delivery = delivery;
		}

		public Delivery Delivery
		{
			get { return _delivery; }
		}

		public abstract void Begin(bool reset = true);
		//public abstract void Import(T deliveryItem);
		public abstract void Commit();
	}
}
