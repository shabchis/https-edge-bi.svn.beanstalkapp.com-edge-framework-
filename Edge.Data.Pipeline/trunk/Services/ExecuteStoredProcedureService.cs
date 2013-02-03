using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Core.Services;
using System.Data.SqlClient;
using Edge.Core.Data;
using System.Text.RegularExpressions;
using System.Reflection;
using Edge.Core.Configuration;
using Edge.Core.Utilities;
using System.Data;
using System.IO;

namespace Edge.Data.Pipeline.Services
{
	public class ExecuteStoredProcedureService : PipelineService
	{
		SqlCommand Cmd;
		bool SendResultByEmail = false;
		protected override void OnInit()
		{
			
			string sp = Instance.Configuration.GetOption("Procedure");


			SendResultByEmail = Convert.ToBoolean(Instance.Configuration.GetOption("SendResultByEmail"));

			// Check for a custom connection string
			string connString = Instance.Configuration.GetOption("ConnectionString");
			SqlConnection conn = new SqlConnection(connString);

			// Check for a custom timeout
			string timeoutStr = Instance.Configuration.GetOption("ConnectionTimeout", false);
			TimeSpan _cmdTimeOut;
			if (TimeSpan.TryParse(timeoutStr, out _cmdTimeOut))
			{
				DataManager.CommandTimeout = (Int32)(_cmdTimeOut.TotalSeconds);
			}

			// Build the command
			Cmd = DataManager.CreateCommand(sp, System.Data.CommandType.StoredProcedure);
			DataManager.ConnectionString = connString;
			Cmd.Connection = conn;

			//Getting SQL Parameters from configuration
			foreach (SqlParameter param in Cmd.Parameters)
			{
				string name = param.ParameterName.Remove(0, 1); 
				string configVal;
				if (!Instance.Configuration.Options.TryGetValue("param." + name, out configVal)) // remove the s from params
				{
					param.Value = DBNull.Value;
					continue;
				}

				// Apply the configuration value, before we check if we need to parse it
				object value = configVal;

				// Dynamic Params
				if (configVal.StartsWith("{") && configVal.EndsWith("}"))
				{
					ServiceInstanceInfo targetInstance = Instance;
					string dynamicParam = configVal.Substring(1, configVal.Length - 2);

					// Go up levels ../../InstanceID
					int levelsUp = Regex.Matches(dynamicParam, @"\.\.\/").Count;
					for (int i = 0; i < levelsUp; i++)
						targetInstance = targetInstance.ParentInstance;

					// Split properties into parts (Configuration.Options.BlahBlah);
					dynamicParam = dynamicParam.Replace("../", string.Empty);
					string[] dynamicParamParts = dynamicParam.Split('.');

					// Get the matching property
					if (dynamicParamParts[0] == "Configuration" && dynamicParamParts.Length > 1)
					{
						if (dynamicParamParts[1] == "Options" && dynamicParamParts.Length > 2)
						{
							// Asked for an option
							value = targetInstance.Configuration.Options[dynamicParamParts[2]];
						}
						else
						{
							// Asked for some other configuration value
							PropertyInfo property = typeof(ServiceElement).GetProperty(dynamicParamParts[1]);
							value = property.GetValue(targetInstance.Configuration, null);
						}
					}
					else if (dynamicParamParts[0] == "TimePeriod") //Getting time period
					{
						PropertyInfo property = typeof(PipelineService).GetProperty(dynamicParamParts[0]);

						if (dynamicParamParts[1] == "Start" && dynamicParamParts.Length >= 2)
						{
							value = this.TimePeriod.Start.ToDateTime();
						}
						else //TimePeriod.End
						{
							value = this.TimePeriod.End.ToDateTime();
						}
					}
					else
					{
						// Asked for an instance value
						PropertyInfo property = typeof(ServiceInstanceInfo).GetProperty(dynamicParamParts[0]);
						if (!property.PropertyType.FullName.Equals("System.DateTime"))
						{
							value = property.GetValue(targetInstance, null);

						}
						else
						{
							value = ((DateTimeSpecification)(property.GetValue(targetInstance, null))).ToDateTime().ToString("yyyyMMdd");
						}
					}
				}

				param.Value = value;
				Log.Write(string.Format("ExecuteStoredProcedureService Value{0} ", value), LogMessageType.Information);
			}

		}

		protected override ServiceOutcome DoPipelineWork()
		{

			DataTable dataTable = new DataTable();

			using (DataManager.Current.OpenConnection())
			{
				DataManager.Current.AssociateCommands(Cmd);
				Log.Write(Cmd.ToString(), LogMessageType.Information);

				#region Sending email
				/*============================================================================*/
				if (SendResultByEmail)
				{
					string topic = Instance.Configuration.GetOption("EmailTopic");
					string returnMsg = string.Empty;

					using (SqlDataReader reader = Cmd.ExecuteReader())
					{
						dataTable.Load(reader);
						if (Cmd.Parameters.Contains("@returnMsg"))
						{
							returnMsg = Cmd.Parameters["@returnMsg"].Value.ToString();
						}
					}

					#region Attaching result as file to email 
					string filePath = string.Empty;
					
					if (Convert.ToBoolean(Instance.Configuration.GetOption("AttachResultAsFile")))
					{
						StringBuilder sb = new StringBuilder();

						foreach (DataColumn col in dataTable.Columns)
						{
							sb.Append(col.ColumnName + '\u0009');
						}

						sb.Remove(sb.Length - 1, 1);
						sb.Append(Environment.NewLine);

						foreach (DataRow row in dataTable.Rows)
						{
							for (int i = 0; i < dataTable.Columns.Count; i++)
							{
								sb.Append(row[i].ToString() + '\u0009');
							}

							sb.Append(Environment.NewLine);
						}

						filePath = Instance.Configuration.GetOption("CsvFilePath")+ "AccountPreformanceReport_" + DateTime.Now.ToString("yyyyMMddHHmmssffff") + this.Instance.AccountID + "_" + Guid.NewGuid()+".csv";
						File.WriteAllText(filePath , sb.ToString(),Encoding.Unicode);
						}
					#endregion

					if (dataTable.Rows.Count > 0 || !string.IsNullOrEmpty(returnMsg)) // only if getting results -> send email.
					{
						if (!string.IsNullOrEmpty(Instance.Configuration.GetOption("EMailCC")) && Instance.Configuration.GetOption("EMailCC").Equals("GetUsersFromDB()"))
						{
							Smtp.SetCc(GetUsersFromDB());
						}
						else
							Smtp.SetCc(Instance.Configuration.GetOption("EMailCC"));

						Smtp.SetFromTo(Instance.Configuration.GetOption("EMailFrom"), Instance.Configuration.GetOption("EMailTo"));
						string htmlBody = CreateHtmlFromTemplate(dataTable, returnMsg);

						Smtp.Send(topic, htmlBody, highPriority: Convert.ToBoolean(Instance.Configuration.GetOption("HighPriority")), IsBodyHtml: true,attachmentPath:string.IsNullOrEmpty(filePath)?null:filePath);
					}


				}
				/*============================================================================*/
				#endregion

				else
					Cmd.ExecuteNonQuery();
			}
			return ServiceOutcome.Success;
		}

		private string CreateHtmlFromTemplate(DataTable dataTable, string returnMsg)
		{
			try
			{
				string template = System.IO.File.ReadAllText(Instance.Configuration.GetOption("HtmlTemplatePath"));

				if (!string.IsNullOrEmpty(returnMsg))
					template = template.Replace("{Result_PlaceHolder}", returnMsg);
				else
					template = template.Replace("{Result_PlaceHolder}", string.Empty);

				string htmlTable = ConvertDataTableToHtmlTable(dataTable);

				if (!string.IsNullOrEmpty(htmlTable))
					template = template.Replace("{DataTable_PlaceHolder}", htmlTable);
				else
					template = template.Replace("{DataTable_PlaceHolder}", string.Empty);

				return template;
			}
			catch (Exception ex)
			{
				throw new Exception("Cannot create Html from template", ex);
			}

		}

		private string GetUsersFromDB()
		{
			throw new NotImplementedException();
		}

		public static string ConvertDataTableToHtmlTable(DataTable targetTable)
		{
			string htmlString = "";

			if (targetTable == null)
			{
				throw new System.ArgumentNullException("targetTable");
			}

			StringBuilder htmlBuilder = new StringBuilder();
			htmlBuilder.Append("<table cellpadding='2' cellspacing='2' border=0 class='dataTable'> ");

			//Create Header Row
			htmlBuilder.Append("<tr>");

			foreach (DataColumn targetColumn in targetTable.Columns)
			{
				htmlBuilder.Append("<th class='first'>");
				htmlBuilder.Append(targetColumn.ColumnName);
				htmlBuilder.Append("</th>");
			}

			htmlBuilder.Append("</tr>");

			//Create Data Rows
			foreach (DataRow myRow in targetTable.Rows)
			{
				htmlBuilder.Append("<tr align='left' valign='top'>");

				foreach (DataColumn targetColumn in targetTable.Columns)
				{
					htmlBuilder.Append("<td align='left' valign='top'>");
					htmlBuilder.Append(myRow[targetColumn.ColumnName].ToString());
					htmlBuilder.Append("</td>");
				}

				htmlBuilder.Append("</tr>");
			}

			//Create String to be Returned
			htmlString = htmlBuilder.ToString();

			return htmlString;
		}

	}
}