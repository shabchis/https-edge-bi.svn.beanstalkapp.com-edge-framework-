using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Data.Pipeline;
using Edge.Data.Pipeline.Services;
using Edge.Data.Objects;

namespace Edge.Data.Pipeline.Services
{
	public class GenericInitializerService: PipelineService
	{
		protected override Core.Services.ServiceOutcome DoPipelineWork()
		{
			if (this.Delivery == null)
			{
				this.Delivery = NewDelivery();
				this.Delivery.TargetPeriod = this.TargetPeriod;
				this.Delivery.Account = this.Instance.AccountID != -1 ? new Account() { ID = this.Instance.AccountID } : null; // no account means there is no permission validation
				this.Delivery.TargetLocationDirectory = this.Instance.Configuration.Options[Const.GenericConfigurationOptions.TargetLocationDirectory];
			}

			this.Delivery.Files.Add(new DeliveryFile()
			{
				Name = this.Instance.Configuration.Options[Const.GenericConfigurationOptions.DeliveryFileName],
				SourceUrl = this.Instance.Configuration.Options[Const.GenericConfigurationOptions.SourceUrl]
			});

			this.Delivery.Save();

			return Core.Services.ServiceOutcome.Success;
		}
	}
}
