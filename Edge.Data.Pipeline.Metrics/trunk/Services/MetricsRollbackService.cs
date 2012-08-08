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
			string[] deliveriesIds=null;
			string[] ouputsIds=null;
			if (Instance.Configuration.Options.ContainsKey(Consts.ConfigurationOptions.RollbackDeliveries))
				deliveriesIds = this.Instance.Configuration.GetOption(Consts.ConfigurationOptions.RollbackDeliveries).Split(',');
			else if (Instance.Configuration.Options.ContainsKey(Consts.ConfigurationOptions.RollbackOutputs))
				ouputsIds = this.Instance.Configuration.GetOption(Consts.ConfigurationOptions.RollbackOutputs).Split(',');
			else
				throw new Exception("Option RollbackDeliveries or RollbackOutputs must be defined");
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
			string checksumThreshold = Instance.Configuration.Options[Consts.ConfigurationOptions.ChecksumTheshold];
			MetricsImportManagerOptions options = new MetricsImportManagerOptions()
			{
				SqlTransformCommand = Instance.Configuration.Options[Consts.AppSettings.SqlTransformCommand],
				SqlStageCommand = Instance.Configuration.Options[Consts.AppSettings.SqlStageCommand],
				SqlRollbackCommand = Instance.Configuration.Options[Consts.AppSettings.SqlRollbackCommand],
				ChecksumThreshold = checksumThreshold == null ? 0.01 : double.Parse(checksumThreshold)
			};
			string checksumThreshold = Instance.Configuration.Options[Consts.ConfigurationOptions.ChecksumTheshold];
			string importManagerTypeName = Instance.Configuration.GetOption(Consts.ConfigurationOptions.ImportManagerType);
			Type importManagerType = Type.GetType(importManagerTypeName);
			var importManager = (MetricsImportManager)Activator.CreateInstance(importManagerType, this.Instance.InstanceID, options);

			
			string[] deliveriesIds = null;
			string[] ouputsIds = null;
			if (Instance.Configuration.Options.ContainsKey(Consts.ConfigurationOptions.RollbackDeliveries))
				deliveriesIds = this.Instance.Configuration.GetOption(Consts.ConfigurationOptions.RollbackDeliveries).Split(',');
			else if (Instance.Configuration.Options.ContainsKey(Consts.ConfigurationOptions.RollbackOutputs))
				ouputsIds = this.Instance.Configuration.GetOption(Consts.ConfigurationOptions.RollbackOutputs).Split(',');
			else
				throw new Exception("Option RollbackDeliveries or RollbackOutputs must be defined");
			
			string tableName = this.Instance.Configuration.GetOption(Consts.ConfigurationOptions.RollbackTableName);
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
