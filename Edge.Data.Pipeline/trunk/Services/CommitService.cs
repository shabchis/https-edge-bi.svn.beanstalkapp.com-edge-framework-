using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Data.SqlClient;
using Edge.Core.Configuration;

namespace Edge.Data.Pipeline.Services
{
	class CommitService : PipelineService
	{	
		protected override Core.Services.ServiceOutcome DoPipelineWork()
		{
			// 1. handle conflictingDeliveris
			HandleConflictingDeliveries();

			string procedureName;
			if (!this.Instance.Configuration.Options.TryGetValue(Consts.DeliverParameters.CommitProcedureName, out procedureName))
				throw new InvalidOperationException(string.Format("Configuration must contains key: {0}", Consts.DeliverParameters.CommitProcedureName));

			string measuresFieldNamesSQL = Delivery.Parameters[Consts.DeliverParameters.MeasuresFieldNamesSQL].ToString();
			string measuresNamesSQL = Delivery.Parameters[Consts.DeliverParameters.MeasuresNamesSQL].ToString();
			string tablePerfix = Delivery.Parameters[Consts.DeliverParameters.TablePerfix].ToString();
			string deliveryId = Delivery.DeliveryID.ToString("N");


			// TODO: CONSTS FOR PARAMETERSNAME AND CONNECTIONSTRING?
			using (SqlConnection deliveriesDBConnection = new SqlConnection(AppSettings.GetConnectionString(this, "DeliveriesDb")))
			{
				deliveriesDBConnection.Open();
				using (SqlCommand command = new SqlCommand(procedureName, deliveriesDBConnection))
				{
					command.Parameters.Add("@DeliveryID", SqlDbType.NVarChar, 4000);
					command.Parameters["@DeliveryID"].Value = deliveryId;

					command.Parameters.Add("@DeliveryTablePrefix", SqlDbType.NVarChar, 4000);
					command.Parameters["@DeliveryTablePrefix"].Value = tablePerfix;

					command.Parameters.Add("@MeasuresNamesSQL", SqlDbType.NVarChar, 4000);
					command.Parameters["@MeasuresNamesSQL"].Value = measuresNamesSQL;

					command.Parameters.Add("@MeasuresFieldNamesSQL", SqlDbType.NVarChar, 4000);
					command.Parameters["@MeasuresFieldNamesSQL"].Value = measuresFieldNamesSQL;

					
					SqlParameter outputCommitTableName=new SqlParameter("@CommitTableName",SqlDbType.NVarChar,4000);
					outputCommitTableName.Direction=ParameterDirection.Output;
					command.Parameters.Add(outputCommitTableName);

					command.ExecuteNonQuery();

					Delivery.Parameters["CommitTableName"] = command.Parameters["@CommitTableName"].Value;

				}
			}

			return Core.Services.ServiceOutcome.Success;
		}
	}
	

}
