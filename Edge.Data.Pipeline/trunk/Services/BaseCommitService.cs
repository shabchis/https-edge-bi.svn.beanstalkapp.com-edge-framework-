using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Data.SqlClient;
using Edge.Core.Configuration;

namespace Edge.Data.Pipeline.Services
{
	public abstract class BaseCommitService : PipelineService
	{
		public abstract DeliveryManager GetDeliveryManager();

		protected sealed override Core.Services.ServiceOutcome DoPipelineWork()
		{
			// Start handling rollback
			DeliveryManager deliveryManager = GetDeliveryManager();
			Delivery unused;
			RollbackOperation rollbackOperation = deliveryManager.HandleConflicts(DeliveryConflictBehavior.Rollback, out unused, true);

			string procedureName;
			if (!this.Instance.Configuration.Options.TryGetValue(Consts.DeliverParameters.CommitProcedureName, out procedureName))
				throw new InvalidOperationException(string.Format("Configuration option required: {0}", Consts.DeliverParameters.CommitProcedureName));

			string measuresFieldNamesSQL = Delivery.Parameters[Consts.DeliverParameters.MeasuresFieldNamesSQL].ToString();
			string measuresNamesSQL = Delivery.Parameters[Consts.DeliverParameters.MeasuresNamesSQL].ToString();
			string tablePerfix = Delivery.Parameters[Consts.DeliverParameters.TablePerfix].ToString();
			string deliveryId = Delivery.DeliveryID.ToString("N");

			using (SqlConnection connection = new SqlConnection(AppSettings.GetConnectionString(this, "DeliveriesDb")))
			{
				connection.Open();
				using (SqlCommand command = new SqlCommand(procedureName, connection))
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

			// If there's a rollback going on, wait for it to end (will throw exceptions if error occured)
			if (rollbackOperation != null)
				rollbackOperation.Wait();

			this.Delivery.History.Add(DeliveryOperation.Comitted, this.Instance.InstanceID);
			this.Delivery.Save();

			return Core.Services.ServiceOutcome.Success;
		}
	}
	

}
