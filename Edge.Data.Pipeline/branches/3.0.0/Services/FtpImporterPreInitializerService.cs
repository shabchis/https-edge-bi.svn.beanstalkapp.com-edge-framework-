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

				FtpWebResponse response = (FtpWebResponse)request.GetResponse();
				StreamReader reader = new StreamReader(response.GetResponseStream());
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
							string SourceUrl = FtpServer + "/" + fileInfo["Name"];

							PipelineServiceConfiguration config = new PipelineServiceConfiguration();

							config.Parameters[Const.DeliveryServiceConfigurationOptions.SourceUrl] = SourceUrl;
							config.Parameters["FileSize"] = fileInfo["Size"];
							config.Parameters["DeliveryFileName"] = fileInfo["Name"];
							config.Parameters["FileModifyDate"] = fileInfo["ModifyDate"];
							
							Environment.ScheduleServiceByName(
								this.Configuration.Parameters.Get<string>("FtpService"),
								this.Configuration.Profile.ProfileID,
								config
								);
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
							Core.Utilities.Log.Write(string.Format("File with same signature already exists in DB,File Signature: {0}", fileSignature), LogMessageType.Warning);
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
