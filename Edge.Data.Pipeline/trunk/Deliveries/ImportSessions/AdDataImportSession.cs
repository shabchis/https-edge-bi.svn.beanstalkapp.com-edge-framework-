using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Data.Pipeline.Objects;

namespace Edge.Data.Pipeline.Deliveries
{

	public class AdDataImportSession : DeliveryImportSession<AdMetricsUnit>, IDisposable
	{
		private Dictionary<Ad, Guid> _adGuids;

		public AdDataImportSession(Delivery delivery): base(delivery)
		{
		}

		public override void Begin(bool reset = true)
		{
			_adGuids = new Dictionary<Ad, Guid>();

			// TODO: setup temp table
			
			throw new NotImplementedException();
		}

		private Guid GetAdGuid(Ad ad)
		{
			Guid guid;

			// try to get an existing guid, or create a new one
			if (!_adGuids.TryGetValue(ad, out guid))
				_adGuids.Add(ad, guid = Guid.NewGuid());

			return guid;
		}

		public void ImportMetrics(AdMetricsUnit metrics)
		{
			Guid adGuid = GetAdGuid(metrics.Ad);

			// TODO: Add to bulk upload? (SqlBulkCopy)
			throw new NotImplementedException();
		}

		public void ImportAd(Ad ad)
		{
			Guid adGuid = GetAdGuid(ad);

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
