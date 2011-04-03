using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Easynet.Edge.Core.Services;
using Easynet.Edge.Core;
using Easynet.Edge.Core.Data;
using System.Data.SqlClient;
using System.Data;
using Easynet.Edge.Core.Utilities;

namespace Services.Data.Pipeline
{

	public class ExampleProcessorService : Service
	{
		DataTable _bufferTable;
		Delivery _delivery;

		private ExampleProcessorService()
		{
			// This must match target table - can also be created automatically with a SELECT
			_bufferTable = new DataTable();
			_bufferTable.Columns.Add("Day_Code");
			_bufferTable.Columns.Add("Account_ID");
			_bufferTable.Columns.Add("Campaign_GK");
			_bufferTable.Columns.Add(); //etc.
			_bufferTable.Columns.Add();
			_bufferTable.Columns.Add();
			_bufferTable.Columns.Add();
		}
		
		protected override ServiceOutcome DoWork()
		{
			// Get the delivery ID either from configuration or from service context
			int deliveryID;
			if (ServiceContext.Contains("DeliveryID"))
				deliveryID = (Int32)Instance.ServiceContext["DeliveryID"];
			else if (Instance.Configuration.Options.Contains("DeliveryID"))
				deliveryID = Int32.Parse(Instance.Configuration.Options["DeliveryID"]);
			else
				throw new ConfigurationException("DeliveryID must be specified in configuration.");

			// Get the delivery created by the retriever
			_delivery = Delivery.Get(deliveryID);

			// Iterate delivery files
			foreach (DeliveryFile file in _delivery.Files.Where(file => file.ReaderType == typeof(ExampleReader)))
			{
				using (DataManager.Current.OpenConnection())
				{
					DataManager.Current.StartTransaction();

					// Constructor gets reference to campaignManager so that the batch knows which rows
					// are dependent on the campaign manager first being comitted
					using (SqlBulkCopy batch = DataManager.CreateBulkCopy(SqlBulkCopyOptions.Default))
					{
						// Dump every 30 rows (get this value from configuration)
						batch.BufferSize = 30;
						batch.DestinationTableName = "SOME_DB_TABLE";

						// Delivery row type can be inherited to add custom transactions
						using (ExampleReader reader = file.CreateReader())
						{
							while (reader.Read())
							{
								batch.WriteToServer(ConvertToDataRow(file, (PpcExampleRow)reader.CurrentRow));
							}
						}
					}

					file.HandledByServices.Add(this.Instance.InstanceID);
					file.Save();

					DataManager.Current.CommitTransaction();
				}

			}
		}

		// Required for working with SqlBulkCopy
		private DataRow ConvertToDataRow(DeliveryFile file, PpcExampleRow row)
		{
			DataRow data = table.NewRow();
			data["Day_Code"] = DayCode.ToDayCode(file.TargetDateTime);
			data["AccountID"] = _delivery.AccountID;
			data["Campaign_GK"] = GKManager.GetCampaignGK(_delivery.AccountID, row.CampaignName);
			data["Adgroup_GK"] = GKManager.GetAdgroupGK(_delivery.AccountID, row.AdgroupName);
			//etc.
		}
	}

}
