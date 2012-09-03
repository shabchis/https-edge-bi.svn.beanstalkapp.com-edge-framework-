using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Data.Pipeline;
using Edge.Data.Pipeline.Services;
using Edge.Data.Objects;
using System.Net;
using System.IO;

namespace Edge.Data.Pipeline.Services
{
	public class UrlInitializerService : PipelineService
	{
		protected override Core.Services.ServiceOutcome DoPipelineWork()
		{
			if (this.Delivery == null)
			{
				int accountID = this.Configuration.Parameters.Get<int>("AccountID", emptyIsError: false, defaultValue: -1);
				int channelID = this.Configuration.Parameters.Get<int>("ChannelID", emptyIsError: false, defaultValue: -1);

				this.Delivery = NewDelivery();
				this.Delivery.TimePeriodDefinition = this.Configuration.TimePeriod.Value;
				this.Delivery.Account = accountID != -1 ? new Account() { ID = accountID } : null;
				this.Delivery.FileDirectory = this.Configuration.Parameters.Get<string>(Const.DeliveryServiceConfigurationOptions.FileDirectory);
				
				if (channelID != -1)
					this.Delivery.Channel = new Channel() { ID = channelID };
			}

			DeliveryFile deliveryFile = new DeliveryFile()
			{
				Name = this.Configuration.Parameters.Get<string>(Const.DeliveryServiceConfigurationOptions.DeliveryFileName, false) ?? "File",
				SourceUrl = this.Configuration.Parameters.Get<string>(Const.DeliveryServiceConfigurationOptions.SourceUrl, false)
			};

			this.Delivery.Files.Add(deliveryFile);

			
			WebRequest request = FileWebRequest.Create(this.Configuration.Parameters.Get<string>("SourceUrl"));
			if (request is FtpWebRequest)
			{
				// FTP Configuration
				this.Delivery.Parameters["UsePassive"] = this.Configuration.Parameters.Get<bool>("UsePassive", emptyIsError: false, defaultValue: true);
				this.Delivery.Parameters["UseBinary"] = this.Configuration.Parameters.Get<bool>("UseBinary");
				this.Delivery.Parameters["SourceUrl"] = this.Configuration.Parameters.Get<string>("SourceUrl");
				this.Delivery.Parameters["UserID"] = this.Configuration.Parameters.Get<string>("UserID");
				this.Delivery.Parameters["Password"] = Core.Utilities.Encryptor.Dec(this.Configuration.Parameters.Get<string>("Password"));
				this.Delivery.Parameters["DirectoryWatcherLocation"] = this.Configuration.Parameters.Get<string>("DirectoryWatcherLocation", false);

				
				deliveryFile.FileSignature = string.Format("{0}-{1}-{2}", this.Configuration.Parameters.Get<object>("DeliveryFileName"),
											this.Configuration.Parameters.Get<object>("FileModifyDate"),
											this.Configuration.Parameters.Get<object>("FileSize"));

				deliveryFile.Parameters.Add("Size", this.Configuration.Parameters.Get<string>("Size", emptyIsError: false));
			}

			this.Delivery.Save();

			return Core.Services.ServiceOutcome.Success;
		}
	}
}
