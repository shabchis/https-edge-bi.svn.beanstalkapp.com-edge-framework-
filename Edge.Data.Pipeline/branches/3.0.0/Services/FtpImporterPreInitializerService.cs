using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.IO;
using Edge.Core.Services;
using System.Data.SqlClient;
using Edge.Core.Configuration;
using Edge.Core.Utilities;

namespace Edge.Data.Pipeline.Services
{
	class FtpImporterPreInitializerService : Service
	{
		protected override ServiceOutcome DoWork()
		{
			// FTP configuration
			string fileConflictBehavior = this.Configuration.Parameters.Get<string>("FileConflictBehavior", emptyIsError: false, defaultValue: "Abort");			
			string FtpServer = this.Configuration.Parameters.Get<string>("FtpServer");
			string[] AllowedExtensions = this.Configuration.Parameters.Get<string>("AllowedExtensions").Split('|');
			bool UsePassive =this.Configuration.Parameters.Get<bool>("UsePassive");
			bool UseBinary = this.Configuration.Parameters.Get<bool>("UseBinary");
			string UserId = this.Configuration.Parameters.Get<string>("UserID");
			string Password = Encryptor.Dec(this.Configuration.Parameters.Get<string>("Password"));

			FtpWebRequest request;
			int filesCounter = 0;

			try
			{
				request = (FtpWebRequest)FtpWebRequest.Create(new Uri(FtpServer + "/"));
				request.UseBinary = UseBinary;
				request.UsePassive = UsePassive;
				request.Credentials = new NetworkCredential(UserId, Password);
				request.Method = WebRequestMethods.Ftp.ListDirectoryDetails;

				var response = (FtpWebResponse)request.GetResponse();
				var reader = new StreamReader(response.GetResponseStream());
				string fileInfoAsString = reader.ReadLine();

				while (fileInfoAsString != null)
				{
					//Checking AllowedExtensions
					Dictionary<string, string> fileInfo = GetFileInfo(fileInfoAsString);
					
					if (fileConflictBehavior == "Ignore"||!CheckFileConflict(fileInfo))
					{
						//Get files with allowed extensions only.

						if (AllowedExtensions.Contains(Path.GetExtension(fileInfo["Name"]), StringComparer.OrdinalIgnoreCase))
						{
							string sourceUrl = FtpServer + "/" + fileInfo["Name"];

							var config = new PipelineServiceConfiguration();
							config.Parameters[Const.DeliveryServiceConfigurationOptions.SourceUrl] = sourceUrl;
							config.Parameters["FileSize"] = fileInfo["Size"];
							config.Parameters["DeliveryFileName"] = fileInfo["Name"];
							config.Parameters["FileModifyDate"] = fileInfo["ModifyDate"];

							// TODO shriat add to scheduler 
							//Environment.ScheduleServiceByName(this.Configuration.Parameters.Get<string>("FtpService"),Configuration.Profile.ProfileID,config);
							var serviceInstance = Environment.NewServiceInstance(Configuration);
							Environment.AddToSchedule(serviceInstance);
						}

					}

					fileInfoAsString = reader.ReadLine();
					filesCounter++;
				}
				reader.Close();
				response.Close();

				if (filesCounter == 0)
				{
					Log("No files in FTP directory for account id " + Configuration.Profile.Parameters["AccountID"], LogMessageType.Information);
				}

			}
			catch (Exception e)
			{
				Log(string.Format("Cannot connect FTP server for account ID:{0}  Exception: {1}", Configuration.Profile.Parameters["AccountID"], e.Message),LogMessageType.Information);
				return ServiceOutcome.Failure;
			}

			return ServiceOutcome.Success;
		}

		private bool CheckFileConflict(Dictionary<string, string> fileInfo)
		{
			var fileSignature = string.Format("{0}-{1}-{2}", fileInfo["Name"], fileInfo["ModifyDate"], fileInfo["Size"]);

			try
			{
				var connection = new SqlConnection(AppSettings.GetConnectionString(typeof(FtpImporterPreInitializerService), "DeliveryDB"));
				using (connection)
				{
					SqlCommand cmd = SqlUtility.CreateCommand(@"DeliveryFile_GetBySignature()", System.Data.CommandType.StoredProcedure);
					var fileSig = new SqlParameter("signature", fileSignature);
					cmd.Parameters.Add(fileSig);
					cmd.Connection = connection;
					connection.Open();
					using (SqlDataReader reader = cmd.ExecuteReader())
					{
						if (reader.Read())
						{
							Log(string.Format("File with same signature already exists in DB,File Signature: {0}", fileSignature), LogMessageType.Warning);
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
			var fileInfo = new Dictionary<string, string>();

			string[] fileInfoAsArray = fileInfoAsString.Split(new[] { " " }, StringSplitOptions.RemoveEmptyEntries);

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
