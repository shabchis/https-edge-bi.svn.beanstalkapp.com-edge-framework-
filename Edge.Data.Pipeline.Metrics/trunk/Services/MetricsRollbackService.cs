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
			string[] ouputsIds = this.Instance.Configuration.GetOption(Consts.ConfigurationOptions.RollbackOutputs).Split(',');
			string spRolebackbyDeliveries = this.Instance.Configuration.GetOption(Consts.ConfigurationOptions.RollbackByDeliverisStoredProc);
			string spRolebackbyOutputs = this.Instance.Configuration.GetOption(Consts.ConfigurationOptions.RollbackByOutputsStoredProc);
			string tableName = this.Instance.Configuration.GetOption(Consts.ConfigurationOptions.RollbackTableName);

			using (SqlConnection conn = new SqlConnection(AppSettings.GetConnectionString(this, Consts.ConnectionStrings.StagingDatabase)))
			{
				SqlTransaction tran = null;
				SqlCommand cmd = null;
				conn.Open();
				if (deliveriesIds != null && deliveriesIds.Length > 0)
				{
					tran = conn.BeginTransaction();
					cmd = DataManager.CreateCommand(spRolebackbyDeliveries, System.Data.CommandType.StoredProcedure);
					cmd.Connection = conn;
					cmd.Transaction = tran;

					foreach (string deliveryID in deliveriesIds)
					{

						cmd.Parameters["@DeliveryID"].Value = deliveryID;
						cmd.Parameters["@TableName"].Value = tableName;
						cmd.ExecuteNonQuery();
					}
				}
				if (ouputsIds != null && ouputsIds.Length > 0)
				{

					cmd = DataManager.CreateCommand(spRolebackbyOutputs, System.Data.CommandType.StoredProcedure);
					cmd.Connection = conn;
					cmd.Transaction = tran;

					foreach (string outputID in ouputsIds)
					{

						cmd.Parameters["@outputID"].Value = outputID;
						cmd.Parameters["@TableName"].Value = tableName;
						cmd.ExecuteNonQuery();
					}
				}

				if (tran != null)
					tran.Commit();


			}


			return Core.Services.ServiceOutcome.Success;
		}


	}
}
