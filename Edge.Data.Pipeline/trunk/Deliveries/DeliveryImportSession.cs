using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Pipeline.Deliveries
{
	public abstract class DeliveryImportSession<T>
	{
		Delivery _delivery;

		public DeliveryImportSession(Delivery delivery)
		{
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
