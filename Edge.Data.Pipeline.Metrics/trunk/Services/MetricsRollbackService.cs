using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Data.Pipeline.Services;
using Edge.Data.Pipeline;
using Edge.Core.Utilities;
using System.Data.SqlClient;
using Edge.Core.Configuration;
using Edge.Core.Data;

namespace Edge.Data.Pipeline.Metrics.Services
{
	public class MetricsRollbackService : PipelineService
	{
		protected override Core.Services.ServiceOutcome DoPipelineWork()
		{
			string[] deliveriesIds = this.Instance.Configuration.GetOption(Consts.ConfigurationOptions.RollbackDeliveries).Split(',');
			string sp = this.Instance.Configuration.GetOption(Consts.ConfigurationOptions.RollbackStoredProc);
			string tableName = this.Instance.Configuration.GetOption(Consts.ConfigurationOptions.RollbackTableName);

			using (SqlConnection conn = new SqlConnection(AppSettings.GetConnectionString(this, Consts.ConnectionStrings.StagingDatabase)))
			{
				conn.Open();
				SqlCommand cmd = DataManager.CreateCommand(sp, System.Data.CommandType.StoredProcedure);
				cmd.Connection = conn;

				foreach (string deliveryID in deliveriesIds)
				{

					cmd.Parameters["@DeliveryID"].Value = deliveryID;
					cmd.Parameters["@TableName"].Value = tableName;
					cmd.ExecuteNonQuery();
				}
			}


			return Core.Services.ServiceOutcome.Success;
		}


	}
}
