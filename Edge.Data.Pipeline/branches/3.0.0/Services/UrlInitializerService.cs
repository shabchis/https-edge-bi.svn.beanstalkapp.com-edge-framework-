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
				this.Delivery.TimePeriodDefinition = this.Configuration.TimePeriod.Value;
				this.Delivery.Account = (int)this.Configuration.Profile.Parameters["AccountID"] != -1 ? new Account() { ID == (int)this.Configuration.Profile.Parameters["AccountID"] } : null; // no account means there is no permission validation
				this.Delivery.FileDirectory = this.Configuration.GetParameter(Const.DeliveryServiceConfigurationOptions.FileDirectory).ToString();

				int channelID = this.Configuration.GetParameter<int>("ChannelID", emptyIsError: false, defaultValue: -1);
				if (channelID != -1)
					this.Delivery.Channel = new Channel()
					{
						ID = channelID
					};
			}
			WebRequest request = FileWebRequest.Create(this.Configuration.GetParameter("SourceUrl",false).ToString());

			/* FTP */
			if (request.GetType().Equals(typeof(FtpWebRequest)))
			{
				/*------------------------------------------------------------------------------------------*/
				#region FTP Configuration
				/*===============================================================================================*/

				
				this.Delivery.Parameters.Add("UsePassive", this.Configuration.GetParameter<bool>("UsePassive",true));



				this.Delivery.Parameters.Add("UseBinary", this.Configuration.GetParameter<bool>("UseBinary"));

				//Get Permissions
				
				string UserId = this.Configuration.GetParameter("UserID").ToString();


				
				string Password = Core.Utilities.Encryptor.Dec(this.Configuration.GetParameter("Password").ToString());
				/*===============================================================================================*/
				#endregion


				this.Delivery.Files.Add(new Data.Pipeline.DeliveryFile()
				{
					Name = this.Configuration.GetParameter("DeliveryFileName").ToString(),
					SourceUrl = this.Configuration.GetParameter(Const.DeliveryServiceConfigurationOptions.SourceUrl).ToString(),
				});

				var fileSignature = string.Format("{0}-{1}-{2}", this.Configuration.GetParameter("DeliveryFileName"),
											this.Configuration.GetParameter("FileModifyDate"),
											this.Configuration.GetParameter("FileSize"));

				this.Delivery.Files[this.Configuration.GetParameter("DeliveryFileName").ToString()].Parameters.Add("Size", this.Configuration.GetParameter("Size"));
				this.Delivery.Parameters["SourceUrl"] = this.Configuration.GetParameter("SourceUrl").ToString();
				this.Delivery.Parameters["UserID"] = UserId;
				this.Delivery.Parameters["Password"] = Password;
				this.Delivery.Parameters["DirectoryWatcherLocation"] = this.Configuration.GetParameter("DirectoryWatcherLocation",false);
				this.Delivery.Files[this.Configuration.GetParameter("DeliveryFileName",false).ToString()].FileSignature = fileSignature;
			}
			else
			{
				this.Delivery.Files.Add(new DeliveryFile()
				{
					Name = this.Configuration.GetParameter(Const.DeliveryServiceConfigurationOptions.DeliveryFileName,false).ToString() ?? "File",
					SourceUrl = this.Configuration.GetParameter(Const.DeliveryServiceConfigurationOptions.SourceUrl,false).ToString()
				});
			}


			this.Delivery.Save();

			return Core.Services.ServiceOutcome.Success;
		}
	}
}
