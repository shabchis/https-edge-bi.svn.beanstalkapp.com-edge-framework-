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
				this.Delivery = NewDelivery();
				this.Delivery.TimePeriodDefinition = this.TimePeriod;
				this.Delivery.Account = this.Instance.AccountID != -1 ? new Account() { ID = this.Instance.AccountID } : null; // no account means there is no permission validation
				this.Delivery.FileDirectory = this.Instance.Configuration.GetOption(Const.DeliveryServiceConfigurationOptions.FileDirectory);

				int channelID = this.Instance.Configuration.GetOption<int>("ChannelID", emptyIsError: false, defaultValue: -1);
				if (channelID != -1)
					this.Delivery.Channel = new Channel()
					{
						ID = channelID
					};
			}
			WebRequest request = FileWebRequest.Create(this.Instance.Configuration.Options["SourceUrl"]);

			/* FTP */
			if (request.GetType().Equals(typeof(FtpWebRequest)))
			{
				/*------------------------------------------------------------------------------------------*/
				#region FTP Configuration
				/*===============================================================================================*/

				if (String.IsNullOrEmpty(this.Instance.Configuration.Options["UsePassive"]))
					throw new Exception("Missing Configuration Param , UsePassive");
				this.Delivery.Parameters.Add("UsePassive", bool.Parse(this.Instance.Configuration.Options["UsePassive"]));

				if (String.IsNullOrEmpty(this.Instance.Configuration.Options["UseBinary"]))
					throw new Exception("Missing Configuration Param , UsePassive");
				this.Delivery.Parameters.Add("UseBinary", bool.Parse(this.Instance.Configuration.Options["UseBinary"]));

				//Get Permissions
				if (String.IsNullOrEmpty(this.Instance.Configuration.Options["UserID"]))
					throw new Exception("Missing Configuration Param , UserID");
				string UserId = this.Instance.Configuration.Options["UserID"];


				if (String.IsNullOrEmpty(this.Instance.Configuration.Options["Password"]))
					throw new Exception("Missing Configuration Param , Password");
				string Password = Core.Utilities.Encryptor.Dec(this.Instance.Configuration.Options["Password"]);
				/*===============================================================================================*/
				#endregion


				this.Delivery.Files.Add(new Data.Pipeline.DeliveryFile()
				{
					Name = this.Instance.Configuration.Options["DeliveryFileName"],
					SourceUrl = this.Instance.Configuration.Options[Const.DeliveryServiceConfigurationOptions.SourceUrl],
				});

				var fileSignature = string.Format("{0}-{1}-{2}", this.Instance.Configuration.Options["DeliveryFileName"],
											this.Instance.Configuration.Options["FileModifyDate"],
											this.Instance.Configuration.Options["FileSize"]);

				this.Delivery.Files[this.Instance.Configuration.Options["DeliveryFileName"]].Parameters.Add("Size", this.Instance.Configuration.Options["Size"]);
				this.Delivery.Parameters["SourceUrl"] = this.Instance.Configuration.Options["SourceUrl"];
				this.Delivery.Parameters["UserID"] = UserId;
				this.Delivery.Parameters["Password"] = Password;
				this.Delivery.Parameters["DirectoryWatcherLocation"] = this.Instance.Configuration.Options["DirectoryWatcherLocation"];
				this.Delivery.Files[this.Instance.Configuration.Options["DeliveryFileName"]].FileSignature = fileSignature;
			}
			else
			{
				this.Delivery.Files.Add(new DeliveryFile()
				{
					Name = this.Instance.Configuration.Options[Const.DeliveryServiceConfigurationOptions.DeliveryFileName] ?? "File",
					SourceUrl = this.Instance.Configuration.GetOption(Const.DeliveryServiceConfigurationOptions.SourceUrl)
				});
			}


			this.Delivery.Save();

			return Core.Services.ServiceOutcome.Success;
		}
	}
}
