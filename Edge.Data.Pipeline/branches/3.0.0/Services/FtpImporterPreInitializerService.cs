using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Data.Objects;
using System.Net;
using System.IO;
using System.Text.RegularExpressions;
using Edge.Core.Services;
using System.Data.SqlClient;
using Edge.Core.Configuration;
using Edge.Core.Utilities;

namespace Edge.Data.Pipeline.Services
{
	class FtpImporterPreInitializerService : Service
	{

		protected override Core.Services.ServiceOutcome DoWork()
		{
			// Instead of
			string fileConflictBehavior = this.Configuration.GetParameter<string>("FileConflictBehavior", emptyIsError: false, defaultValue: "Abort");

			//string fileConflictBehavior = "Abort";
			//if (!String.IsNullOrEmpty(this.Instance.Configuration.Options["FileConflictBehavior"]))
				//fileConflictBehavior = this.Instance.Configuration.Options["FileConflictBehavior"];

			#region FTP Configuration
			/*===============================================================================================*/
			
			string FtpServer = this.Configuration.GetParameter("FtpServer").ToString();


			
			string[] AllowedExtensions = this.Configuration.GetParameter("AllowedExtensions").ToString().Split('|');

		
			bool UsePassive =this.Configuration.GetParameter<bool>("UsePassive");

			
			bool UseBinary = this.Configuration.GetParameter<bool>("UseBinary");

		
			string UserId = this.Configuration.GetParameter("UserID").ToString();


			
			string Password = Core.Utilities.Encryptor.Dec(this.Configuration.GetParameter("Password").ToString());
			/*===============================================================================================*/
			#endregion
			FtpWebRequest request;
			int filesCounter = 0;

			try
			{
				request = (FtpWebRequest)FtpWebRequest.Create(new Uri(FtpServer + "/"));
				request.UseBinary = UseBinary;
				request.UsePassive = UsePassive;
				request.Credentials = new NetworkCredential(UserId, Password);
				request.Method = WebRequestMethods.Ftp.ListDirectoryDetails;

				FtpWebResponse response = (FtpWebResponse)request.GetResponse();
				StreamReader reader = new StreamReader(response.GetResponseStream());
				string fileInfoAsString = reader.ReadLine();

				while (fileInfoAsString != null)
				{
					//Checking AllowedExtensions
					Dictionary<string, string> fileInfo = GetFileInfo(fileInfoAsString);
					

					if ((fileConflictBehavior.Equals("Ignore"))||(!CheckFileConflict(fileInfo)))
					{
						//Get files with allowed extensions only.

						if (AllowedExtensions.Contains(Path.GetExtension(fileInfo["Name"]), StringComparer.OrdinalIgnoreCase))
						{
							string SourceUrl = FtpServer + "/" + fileInfo["Name"];

							System.ServiceModel.ChannelFactory<Edge.Core.Scheduling.IScheduleManager> c = new System.ServiceModel.ChannelFactory<Core.Scheduling.IScheduleManager>("shaybarchen");
							c.Open();
							IScheduleManager s = c.CreateChannel();
							Core.SettingsCollection options = new Core.SettingsCollection();

							this.Configuration.Parameters[Const.DeliveryServiceConfigurationOptions.SourceUrl] = SourceUrl;
							this.Configuration.Parameters["FileSize"] = fileInfo["Size"];
							this.Configuration.Parameters["DeliveryFileName"] = fileInfo["Name"];
							this.Configuration.Parameters["FileModifyDate"] = fileInfo["ModifyDate"];
							
							s.AddToSchedule(this.Configuration.GetParameter("FtpService").ToString(), this.Configuration.Profile.Parameters["AccountID"], this.TimeScheduled, this.Configuration.Parameters);
						}

					}

					fileInfoAsString = reader.ReadLine();
					filesCounter++;
				}
				reader.Close();
				response.Close();

				if (filesCounter == 0)
				{
					Core.Utilities.Log.Write("No files in FTP directory for account id " + this.Configuration.Profile.Parameters["AccountID"].ToString(),LogMessageType.Information);
				}

			}
			catch (Exception e)
			{
				Core.Utilities.Log.Write(
					string.Format("Cannot connect FTP server for account ID:{0}  Exception: {1}",
					this.Configuration.Profile.Parameters["AccountID"].ToString(), e.Message),
					LogMessageType.Information);
				return Edge.Core.Services.ServiceOutcome.Failure;
			}

			return Core.Services.ServiceOutcome.Success;
		}

		private bool CheckFileConflict(Dictionary<string, string> fileInfo)
		{
			string fileSignature = string.Format("{0}-{1}-{2}", fileInfo["Name"], fileInfo["ModifyDate"], fileInfo["Size"]);

			SqlConnection connection;

			

			try
			{
				connection = new SqlConnection(AppSettings.GetConnectionString(typeof(FtpImporterPreInitializerService), "DeliveryDB"));
				using (connection)
				{
					SqlCommand cmd = SqlUtility.CreateCommand(@"DeliveryFile_GetBySignature()", System.Data.CommandType.StoredProcedure);
					SqlParameter fileSig = new SqlParameter("signature", fileSignature);
					cmd.Parameters.Add(fileSig);
					cmd.Connection = connection;
					connection.Open();
					using (SqlDataReader reader = cmd.ExecuteReader())
					{
						if (reader.Read())
						{
							Core.Utilities.Log.Write(string.Format("File with same signature already exists in DB,File Signature: {0}", fileSignature), Core.Utilities.LogMessageType.Warning);
							return true;
						}

					}
				}
			}
			catch (Exception ex)
			{
				throw new Exception("Error while trying to get files signature from DB", ex);
			}

			return false;

		}

		private Dictionary<string, string> GetFileInfo(string fileInfoAsString)
		{
			Dictionary<string, string> fileInfo = new Dictionary<string, string>();

			string[] fileInfoAsArray = fileInfoAsString.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);

			fileInfo.Add("Name", fileInfoAsArray.Last());
			string month = fileInfoAsArray[5];
			string day = fileInfoAsArray[6];
			string year = fileInfoAsArray[7];
			fileInfo.Add("ModifyDate", string.Format("{0}-{1}-{2}", day, month, year));
			fileInfo.Add("Size", fileInfoAsArray[4]);
			return fileInfo;
		}
	}
}
