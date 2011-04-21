using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Data.Pipeline.Objects;

namespace Edge.Data.Pipeline.Deliveries
{

	public class AdMetricsImportSession : DeliveryImportSession<AdMetricsUnit>, IDisposable
	{
		public AdMetricsImportSession(Delivery delivery)
			: base(delivery)
		{
		}

		public override void Begin()
		{
			// TODO: setup temp table
			
			throw new NotImplementedException();
		}

		public override void Import(AdMetricsUnit deliveryItem)
		{
			// TODO: Add to bulk upload? (SqlBulkCopy)
			throw new NotImplementedException();
		}

		public override void Commit()
		{
			// TODO: Fwd-only activate stored procedue
			throw new NotImplementedException();
		}

		public void Dispose()
		{
			// TODO: clean up temp shit
			throw new NotImplementedException();
		}
	}
}
