using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Db4objects.Db4o.Linq;

namespace Edge.Data.Pipeline.Services
{
	public class RollbackService: PipelineService
	{
		protected override Core.Services.ServiceOutcome DoPipelineWork()
		{
			if (this.TargetDeliveryID != Guid.Empty)
			{
				// TODO: execute rollback SP
			}
			else
			{
				// Find deliveries matching this service pipeline
				//this.
			}
		}

		public virtual Guid GetDeliveryID()
		{
			// TODO: find delivery ID by parameters
			string fbaccount = this.Instance.Configuration.Options["FBaccount"];

			using (var client = DeliveryDBClient.Connect())
			{
				var results = from Delivery d in client where d.Parameters["FBaccount"] == fbaccount select d.DeliveryID;
			}
		}
	}
}
