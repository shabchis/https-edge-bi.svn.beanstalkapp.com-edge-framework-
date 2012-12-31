using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Data.Pipeline.Metrics.Base;
using Edge.Data.Pipeline.Services;
using Edge.Data.Pipeline;
using Edge.Core.Utilities;
using System.Data.SqlClient;
using Edge.Core.Configuration;

namespace Edge.Data.Pipeline.Metrics.Services
{
	public class MetricsRollbackService : PipelineService
	{
		protected override Core.Services.ServiceOutcome DoPipelineWork()
		{
			string[] deliveriesIds=null;
			string[] ouputsIds=null;
			if (Configuration.Options.ContainsKey(Consts.ConfigurationOptions.RollbackDeliveries))
				deliveriesIds = this.Configuration.GetOption(Consts.ConfigurationOptions.RollbackDeliveries).Split(',');
			else if (Configuration.Options.ContainsKey(Consts.ConfigurationOptions.RollbackOutputs))
				ouputsIds = this.Configuration.GetOption(Consts.ConfigurationOptions.RollbackOutputs).Split(',');
			else
				throw new Exception("Option RollbackDeliveries or RollbackOutputs must be defined");
			string spRolebackbyDeliveries = this.Configuration.GetOption(Consts.ConfigurationOptions.RollbackByDeliverisStoredProc);
			string spRolebackbyOutputs = this.Configuration.GetOption(Consts.ConfigurationOptions.RollbackByOutputsStoredProc);
			string tableName = this.Configuration.GetOption(Consts.ConfigurationOptions.RollbackTableName);




			using (SqlConnection conn = new SqlConnection(AppSettings.GetConnectionString(this, Consts.ConnectionStrings.Staging)))
			{
				SqlTransaction tran = null;
				SqlCommand cmd = null;
				conn.Open();
				if (deliveriesIds != null && deliveriesIds.Length > 0)
				{
					tran = conn.BeginTransaction();
					cmd = SqlUtility.CreateCommand(spRolebackbyDeliveries, System.Data.CommandType.StoredProcedure);
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

					cmd = SqlUtility.CreateCommand(spRolebackbyOutputs, System.Data.CommandType.StoredProcedure);
					cmd.Connection = conn;
					cmd.Transaction = tran;

					foreach (string outputID in ouputsIds)
					{

						cmd.Parameters["@DeliveryOutputID"].Value = outputID;
						cmd.Parameters["@TableName"].Value = tableName;
						cmd.ExecuteNonQuery();
					}
				}

				if (tran != null)
					tran.Commit();


			}


			return Core.Services.ServiceOutcome.Success;

			///for new db
			////*

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


	}
}
