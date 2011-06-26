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
			return Core.Services.ServiceOutcome.Success;
		}

		
	}
}
