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
				string name = param.ParameterName.Remove(0, 1); //fix by alon on 20/1/2001 remove the '@'
				string configVal;
				if (!Instance.Configuration.Options.TryGetValue("param." + name, out configVal)) // remove the s from params
					continue;

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
						//Alon & Shay Bug fix = incorrect date format ( daycode )
						PropertyInfo property = typeof(ServiceInstanceInfo).GetProperty(dynamicParamParts[0]);
						if (!property.PropertyType.FullName.Equals("System.DateTime"))
						{
							value = property.GetValue(targetInstance, null);

						}
						else
						{

							value = ((DateTimeSpecification)(property.GetValue(targetInstance, null))).ToDateTime().ToString("yyyyMMdd");
							// Log.Write("Delete Days - DayCode " + (string)value, LogMessageType.Information);
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
					string Topic = Instance.Configuration.GetOption("EmailTopic");
					using (SqlDataReader reader = Cmd.ExecuteReader())
					{
						dataTable.Load(reader);
					}

					if (!string.IsNullOrEmpty(Instance.Configuration.GetOption("EMailCC")) && Instance.Configuration.GetOption("EMailCC").Equals("GetUsersFromDB()"))
					{
						Smtp.SetCc(GetUsersFromDB());
					}
					else
						Smtp.SetCc(Instance.Configuration.GetOption("EMailCC"));

					Smtp.SetFromTo(Instance.Configuration.GetOption("EMailFrom"), Instance.Configuration.GetOption("EMailTo"));
					string htmlBody = ConvertDataTableToHtml(dataTable);
					Smtp.Send(Topic, htmlBody, highPriority: true, IsBodyHtml: true);

				}
				/*============================================================================*/
				#endregion

				else
					Cmd.ExecuteNonQuery();
			}
			return ServiceOutcome.Success;
		}

		private string GetUsersFromDB()
		{
			throw new NotImplementedException();
		}

		public static string ConvertDataTableToHtml(DataTable targetTable)
		{
			string htmlString = "";

			if (targetTable == null)
			{
				throw new System.ArgumentNullException("targetTable");
			}

			StringBuilder htmlBuilder = new StringBuilder();

			//Create Top Portion of HTML Document
			htmlBuilder.Append("<html>");
			htmlBuilder.Append("<head>");
			//htmlBuilder.Append("<title>");
			//htmlBuilder.Append("Page-");
			//htmlBuilder.Append(Guid.NewGuid().ToString());
			//htmlBuilder.Append("</title>");
			htmlBuilder.Append("</head>");
			htmlBuilder.Append("<body>");
			htmlBuilder.Append("<table border='1px' cellpadding='5' cellspacing='0' ");
			htmlBuilder.Append("style='border: solid 1px Black; font-size: small;'>");

			//Create Header Row
			htmlBuilder.Append("<tr align='left' valign='top'>");

			foreach (DataColumn targetColumn in targetTable.Columns)
			{
				htmlBuilder.Append("<td align='left' valign='top'>");
				htmlBuilder.Append(targetColumn.ColumnName);
				htmlBuilder.Append("</td>");
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

			//Create Bottom Portion of HTML Document
			htmlBuilder.Append("</table>");
			htmlBuilder.Append("</body>");
			htmlBuilder.Append("</html>");

			//Create String to be Returned
			htmlString = htmlBuilder.ToString();

			return htmlString;
		}

	}
}