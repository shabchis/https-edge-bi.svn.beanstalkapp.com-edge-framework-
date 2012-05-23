using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Data.Pipeline;
using Edge.Data.Pipeline.Services;
using Edge.Data.Objects;

namespace Edge.Data.Pipeline.Services
{
	public class UrlInitializerService: PipelineService
	{
		protected override Core.Services.ServiceOutcome DoPipelineWork()
		{
			if (this.Delivery == null)
			{
				this.Delivery = NewDelivery();
				this.Delivery.TimePeriodDefinition = this.TimePeriod;
				this.Delivery.Account = this.Instance.AccountID != -1 ? new Account() { ID = this.Instance.AccountID } : null; // no account means there is no permission validation
				this.Delivery.FileDirectory = this.Instance.Configuration.GetOption(Const.DeliveryServiceConfigurationOptions.TargetLocationDirectory);

				int channelID = this.Instance.Configuration.GetOption<int>("ChannelID", emptyIsError: false, defaultValue: -1);
				if (channelID != -1)
				this.Delivery.Channel = new Channel()
				{
					ID = channelID
				};

				Delivery.CreateSignature(String.Format("UrlInitializerService-[{0}]-[{1}]-[{2}]",
				this.Instance.AccountID,
				this.TimePeriod.ToAbsolute()
				));
			}

			this.Delivery.Files.Add(new DeliveryFile()
			{
				Name = this.Instance.Configuration.Options[Const.DeliveryServiceConfigurationOptions.DeliveryFileName] ?? "File",
				SourceUrl = this.Instance.Configuration.GetOption(Const.DeliveryServiceConfigurationOptions.SourceUrl)
			});

			this.Delivery.Save();

			return Core.Services.ServiceOutcome.Success;
		}
	}
}
