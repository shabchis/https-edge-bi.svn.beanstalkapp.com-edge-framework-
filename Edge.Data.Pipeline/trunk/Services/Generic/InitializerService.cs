using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Data.Pipeline;
using Edge.Data.Pipeline.Services;
using Edge.Data.Objects;

namespace Edge.Data.Pipeline.Services.Generic
{
	public class InitializerService: PipelineService
	{
		protected override Core.Services.ServiceOutcome DoPipelineWork()
		{
			this.Delivery = NewDelivery();

			// No account means there is no permission validation
			this.Delivery.Account = this.Instance.AccountID != -1 ? new Account() { ID = this.Instance.AccountID } : null;

			this.Delivery.Files.Add(new DeliveryFile()
			{
				SourceUrl = this.Instance.Configuration.Options[Const.ConfigurationOptions.SourceUrl]
			});

			this.Delivery.TargetLocationDirectory = this.Instance.Configuration.Options[Const.ConfigurationOptions.TargetLocationDirectory];
			this.Delivery.Save();

			return Core.Services.ServiceOutcome.Success;
		}
	}
}
