using System;
using Edge.Core.Services;
using Edge.Data.Pipeline.Metrics.Misc;
using Edge.Data.Pipeline.Metrics.Services.Configuration;
using Edge.Data.Pipeline.Services;
using Edge.Core.Utilities;
using System.Data.SqlClient;
using Edge.Core.Configuration;

namespace Edge.Data.Pipeline.Metrics.Services
{
	public class MetricsRollbackService : PipelineService
	{
		#region Properties
		public new MetricsRollbackServiceconfiguration Configuration
		{
			get { return (MetricsRollbackServiceconfiguration)base.Configuration; }
		} 
		#endregion

		#region Override DoWork
		protected override ServiceOutcome DoPipelineWork()
		{
			// takes deliveries or outputs to rollback from Config
			var deliveriesIds = Configuration.Deliveries.Split(',');
			var ouputsIds = Configuration.Deliveries.Split(',');

			if (deliveriesIds.Length == 0 && ouputsIds.Length == 0)
				throw new Exception("Option RollbackDeliveries or RollbackOutputs must be defined");

			// start Rollback
			using (var conn = new SqlConnection(AppSettings.GetConnectionString(this, Consts.ConnectionStrings.Staging)))
			{
				conn.Open();
				var tran = conn.BeginTransaction();

				// rollback deliveries
				if (deliveriesIds.Length > 0)
				{
					var cmd = SqlUtility.CreateCommand(Configuration.RollbackDeliveriesStoredProc, System.Data.CommandType.StoredProcedure);
					cmd.Connection = conn;
					cmd.Transaction = tran;

					foreach (var deliveryID in deliveriesIds)
					{
						cmd.Parameters["@DeliveryID"].Value = deliveryID;
						cmd.Parameters["@TableName"].Value = Configuration.TableName;
						cmd.ExecuteNonQuery();
					}
				}
				// rollback outputs
				if (ouputsIds.Length > 0)
				{
					var cmd = SqlUtility.CreateCommand(Configuration.RollbackOutputsStoredProc, System.Data.CommandType.StoredProcedure);
					cmd.Connection = conn;
					cmd.Transaction = tran;

					foreach (var outputID in ouputsIds)
					{
						cmd.Parameters["@DeliveryOutputID"].Value = outputID;
						cmd.Parameters["@TableName"].Value = Configuration.TableName;
						cmd.ExecuteNonQuery();
					}
				}

				// commit transaction
				tran.Commit();
			}

			return ServiceOutcome.Success;

			//for new db

			/*
			string checksumThreshold = Configuration.Parameters.Get<T>(Consts.ConfigurationOptions.ChecksumTheshold];
			MetricsImportManagerOptions options = new MetricsImportManagerOptions()
			{
				SqlTransformCommand = Configuration.Parameters.Get<T>(Consts.AppSettings.SqlTransformCommand],
				SqlStageCommand = Configuration.Parameters.Get<T>(Consts.AppSettings.SqlStageCommand],
				SqlRollbackCommand = Configuration.Parameters.Get<T>(Consts.AppSettings.SqlRollbackCommand],
				ChecksumThreshold = checksumThreshold == null ? 0.01 : double.Parse(checksumThreshold)
			};
			string checksumThreshold = Configuration.Parameters.Get<T>(Consts.ConfigurationOptions.ChecksumTheshold];
			string importManagerTypeName = Configuration.GetOption(Consts.ConfigurationOptions.ImportManagerType);
			Type importManagerType = Type.GetType(importManagerTypeName);
			var importManager = (MetricsImportManager)Activator.CreateInstance(importManagerType, this.InstanceID, options);

			
			string[] deliveriesIds = null;
			string[] ouputsIds = null;
			if (Configuration.Options.ContainsKey(Consts.ConfigurationOptions.RollbackDeliveries))
				deliveriesIds = this.Configuration.GetOption(Consts.ConfigurationOptions.RollbackDeliveries).Split(',');
			else if (Configuration.Options.ContainsKey(Consts.ConfigurationOptions.RollbackOutputs))
				ouputsIds = this.Configuration.GetOption(Consts.ConfigurationOptions.RollbackOutputs).Split(',');
			else
				throw new Exception("Option RollbackDeliveries or RollbackOutputs must be defined");
			
			string tableName = this.Configuration.GetOption(Consts.ConfigurationOptions.RollbackTableName);
			List<DeliveryOutput> outputs=new List<DeliveryOutput>();
			List<Delivery> Deliveries=new List<Delivery>();
			if (deliveriesIds != null && deliveriesIds.Length > 0)
			{
				foreach (var id in deliveriesIds)
				{
					Deliveries.Add(Delivery.Get(Guid.Parse(id)));
					
				}
				importManager.RollbackDeliveries(Deliveries.ToArray());

			}
			if (ouputsIds != null && ouputsIds.Length > 0)
			{
				foreach (var id in ouputsIds)
				{
				outputs.Add(DeliveryOutput.Get(Guid.Parse(id)));
				}
				importManager.RollbackOutputs(outputs.ToArray());
			}
			return Core.Services.ServiceOutcome.Success; */
		} 
		#endregion
	}
}
