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
				this.Delivery.FileDirectory = this.Instance.Configuration.GetOption(Const.DeliveryServiceConfigurationOptions.TargetLocationDirectory);

				int channelID = this.Instance.Configuration.GetOption<int>("ChannelID", emptyIsError: false, defaultValue: -1);
				if (channelID != -1)
					this.Delivery.Channel = new Channel()
					{
						ID = channelID
					};
			}

			/*------------------------------------------------------------------------------------------*/
			#region FTP Configuration
			/*===============================================================================================*/
			if (String.IsNullOrEmpty(this.Instance.Configuration.Options["FtpServer"]))
				throw new Exception("Missing Configuration Param , FtpServer");
			string FtpServer = this.Instance.Configuration.Options["FtpServer"];


			//Get AllowedExtensions
			if (String.IsNullOrEmpty(this.Instance.Configuration.Options["AllowedExtensions"]))
				throw new Exception("Missing Configuration Param , AllowedExtensions");
			string[] AllowedExtensions = this.Instance.Configuration.Options["AllowedExtensions"].Split('|');

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

			if (!String.IsNullOrEmpty(FtpServer))
			{
				this.Delivery.Files.Add(new Data.Pipeline.DeliveryFile()
				{
					Name = "FTP_" + this.Instance.Configuration.Options["FileName"],
					SourceUrl = this.Instance.Configuration.Options[Const.DeliveryServiceConfigurationOptions.SourceUrl],
				});

				this.Delivery.Files["FTP_" + this.Instance.Configuration.Options["FileName"]].Parameters.Add("Size", this.Instance.Configuration.Options["Size"]);
				this.Delivery.Parameters["FtpServer"] = FtpServer;
				this.Delivery.Parameters["AllowedExtensions"] = AllowedExtensions;
				this.Delivery.Parameters["UserID"] = UserId;
				this.Delivery.Parameters["Password"] = Password;
				this.Delivery.Parameters["DirectoryWatcherLocation"] = this.Instance.Configuration.Options["DirectoryWatcherLocation"];

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
