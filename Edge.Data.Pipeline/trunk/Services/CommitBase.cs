using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Data.SqlClient;
using Edge.Core.Configuration;
using Edge.Core.Data;

namespace Edge.Data.Pipeline.Services
{
	public abstract class CommitBase : PipelineService
	{
		public abstract DeliveryManager GetDeliveryManager();

		protected sealed override Core.Services.ServiceOutcome DoPipelineWork()
		{
			// Start handling rollback
			DeliveryManager deliveryManager = GetDeliveryManager();
			Delivery unused;
			RollbackOperation rollbackOperation = deliveryManager.HandleConflicts(DeliveryConflictBehavior.Rollback, out unused, true);

			string prepareCmdText;
			if (!this.Instance.Configuration.Options.TryGetValue("PrepareSqlCommand", out prepareCmdText))
				throw new InvalidOperationException(string.Format("Configuration option required: {0}", "PrepareSqlCommand"));
			string commitCmdText;
			if (!this.Instance.Configuration.Options.TryGetValue("CommitSqlCommand", out commitCmdText))
				throw new InvalidOperationException(string.Format("Configuration option required: {0}", "CommitSqlCommand"));

			DeliveryHistoryEntry processedEntry = this.Delivery.History.Last(entry => entry.Operation == DeliveryOperation.Processed);
			if (processedEntry == null)
				throw new Exception("This delivery has not been processed yet (could not find a 'processed' history entry).");

			DeliveryHistoryEntry commitEntry = new DeliveryHistoryEntry(DeliveryOperation.Comitted, this.Instance.InstanceID, new Dictionary<string,object>());

			// get this from last 'Processed' history entry
			string measuresFieldNamesSQL = processedEntry.Parameters[Consts.DeliveryHistoryParameters.MeasureOltpFieldsSql].ToString();
			string measuresNamesSQL = processedEntry.Parameters[Consts.DeliveryHistoryParameters.MeasureNamesSql].ToString();
			string tablePerfix = processedEntry.Parameters[Consts.DeliveryHistoryParameters.TablePerfix].ToString();
			string deliveryId = this.Delivery.DeliveryID.ToString("N");
			string commitTableName;

			// ...........................
			// FINALIZE data
			using (SqlConnection connection = new SqlConnection(AppSettings.GetConnectionString(this, "DeliveriesDb")))
			{
				connection.Open();
				using (SqlCommand command = DataManager.CreateCommand(prepareCmdText, CommandType.StoredProcedure))
				{
					command.Parameters["@DeliveryID"].Size = 4000;
					command.Parameters["@DeliveryID"].Value = deliveryId;

					command.Parameters["@DeliveryTablePrefix"].Size = 4000;
					command.Parameters["@DeliveryTablePrefix"].Value = tablePerfix;

					command.Parameters["@MeasuresNamesSQL"].Size = 4000;
					command.Parameters["@MeasuresNamesSQL"].Value = measuresNamesSQL;

					command.Parameters["@MeasuresFieldNamesSQL"].Size = 4000;
					command.Parameters["@MeasuresFieldNamesSQL"].Value = measuresFieldNamesSQL;

					command.Parameters["@CommitTableName"].Size = 4000;

					command.ExecuteNonQuery();

					commitEntry.Parameters["CommitTableName"] = commitTableName = command.Parameters["@CommitTableName"].Value.ToString();

				}
			}

			// ...........................
			// WAIT FOR ROLLBACK TO END
			// If there's a rollback going on, wait for it to end (will throw exceptions if error occured)
			if (rollbackOperation != null)
			{
				rollbackOperation.Wait();
			}

			// ...........................
			// COMMIT data to OLTP
			using (SqlConnection connection = new SqlConnection(AppSettings.GetConnectionString(this, "DeliveriesDb")))
			{
				connection.Open();
				using (SqlCommand command = DataManager.CreateCommand(commitCmdText, CommandType.StoredProcedure))
				{
					command.Connection = connection;

					command.Parameters["@DeliveryID"].Size = 4000;
					command.Parameters["@DeliveryID"].Value = deliveryId;

					command.Parameters["@DeliveryTablePrefix"].Size = 4000;
					command.Parameters["@DeliveryTablePrefix"].Value = tablePerfix;

					command.Parameters["@MeasuresNamesSQL"].Size = 4000;
					command.Parameters["@MeasuresNamesSQL"].Value = measuresNamesSQL;

					command.Parameters["@MeasuresFieldNamesSQL"].Size = 4000;
					command.Parameters["@MeasuresFieldNamesSQL"].Value = measuresFieldNamesSQL;

					command.ExecuteNonQuery();
				}
			}

			this.Delivery.History.Add(commitEntry);
			this.Delivery.Save();

			return Core.Services.ServiceOutcome.Success;
		}
	}
	

}
