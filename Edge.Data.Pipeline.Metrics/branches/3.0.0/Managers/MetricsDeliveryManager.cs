using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using Edge.Core.Configuration;
using Edge.Core.Utilities;
using Edge.Data.Objects;
using Edge.Data.Pipeline.Metrics.Misc;
using Edge.Data.Pipeline.Objects;

namespace Edge.Data.Pipeline.Metrics.Managers
{
	/// <summary>
	/// Responsible for delivery processing: import data, staging, rollback, etc.
	/// </summary>
	public class MetricsDeliveryManager : DeliveryManager
	{
		#region Data Members
		private readonly SqlConnection _deliverySqlConnection;
		private readonly SqlConnection _objectsSqlConnection;

		public MetricsDeliveryManagerOptions Options { get; private set; }

		private string _tablePrefix;
		private readonly MetricsTableManager _metricsTableManager;
		private readonly EdgeObjectsManager _edgeObjectsManager;
		private IdentityManager _identityManager;
		#endregion

		#region Constructors
		public MetricsDeliveryManager(Guid serviceInstanceID, Dictionary<string,EdgeType> edgeTypes = null, MetricsDeliveryManagerOptions options = null)
			: base(serviceInstanceID)
		{
			options = options ?? new MetricsDeliveryManagerOptions();
			options.StagingConnectionString = options.StagingConnectionString ?? AppSettings.GetConnectionString(this, Consts.ConnectionStrings.Staging);
			options.CommitConnectionString = options.CommitConnectionString ?? AppSettings.GetConnectionString(this, Consts.ConnectionStrings.DataWarehouse);
			options.ObjectsConnectionString = options.CommitConnectionString ?? AppSettings.GetConnectionString(this, Consts.ConnectionStrings.Objects);

			options.SqlTransformCommand = options.SqlTransformCommand ?? AppSettings.Get(this, Consts.AppSettings.SqlTransformCommand, throwException: false);
			options.SqlStageCommand = options.SqlStageCommand ?? AppSettings.Get(this, Consts.AppSettings.SqlStageCommand, throwException: false);
			options.SqlCommitCommand = options.SqlStageCommand ?? AppSettings.Get(this, Consts.AppSettings.SqlCommitCommand, throwException: false);
			options.SqlRollbackCommand = options.SqlRollbackCommand ?? AppSettings.Get(this, Consts.AppSettings.SqlRollbackCommand, throwException: false);

			Options = options;
			
			// create connection and table managers
			_deliverySqlConnection = OpenDbConnection(Consts.ConnectionStrings.Deliveries);
			_objectsSqlConnection = OpenDbConnection(Consts.ConnectionStrings.Objects);

			_edgeObjectsManager = new EdgeObjectsManager(_deliverySqlConnection, _objectsSqlConnection) {EdgeTypes = edgeTypes};
			_metricsTableManager = new MetricsTableManager(_deliverySqlConnection, _edgeObjectsManager);
		}

		#endregion

		#region Import
		/*=========================*/
		
		protected override void OnBeginImport(MetricsUnit sampleMetrics)
		{
			// set table prefix
			_tablePrefix = string.Format("{0}_{1}_{2}_{3}", CurrentDelivery.Account.ID, CurrentDelivery.Name, DateTime.Now.ToString("yyyMMdd_HHmmss"), CurrentDelivery.DeliveryID.ToString("N").ToLower());
			CurrentDelivery.Parameters[Consts.DeliveryHistoryParameters.TablePerfix] = _tablePrefix;

			// create delivery object tables (should be Usid instead of GK)
			_edgeObjectsManager.CreateDeliveryObjectTables(_tablePrefix);
			
			// create metrics table using metrics table manager and sample metrics
			_metricsTableManager.CreateDeliveryMetricsTable(_tablePrefix, sampleMetrics);
			
			// store delivery and staging (best match) table names in delivery
			CurrentDelivery.Parameters[Consts.DeliveryHistoryParameters.DeliveryMetricsTableName] = _metricsTableManager.TableName;
			CurrentDelivery.Parameters[Consts.DeliveryHistoryParameters.StagingMetricsTableName] = _metricsTableManager.FindStagingTable();
			
			// CHECKSUMMANAGER: setup

			// MAPPER: setup bulks for objects and metrics
		}

		public virtual void ImportMetrics(MetricsUnit metrics)
		{
			EnsureBeginImport();

			_metricsTableManager.ImportMetrics(metrics);
		}

		protected override void OnEndImport()
		{
			// insert all objects into DB
			_edgeObjectsManager.ImportObjects(_tablePrefix);
		}

		protected void EnsureBeginImport()
		{
			if (State != DeliveryManagerState.Importing)
				throw new InvalidOperationException("BeginImport must be called before anything can be imported.");
		}
		
		/*=========================*/
		#endregion

		#region Transform
		/*=========================*/

		const int TRANSFORM_PASS_IDENTITY = 0;
		const int TRANSFORM_PASS_CHECKSUM = 1;
		const int TRANSFORM_PASS_CURRENCY = 2;

		protected override int TransformPassCount
		{
			get { return 3; }
		}

		protected override void OnBeginTransform()
		{
			if (String.IsNullOrWhiteSpace(Options.SqlTransformCommand))
				throw new DeliveryManagerException("Options.SqlTransformCommand is empty.");

			_identityManager = new IdentityManager(_deliverySqlConnection, _objectsSqlConnection);
		}

		protected override void OnTransform(Delivery delivery, int pass)
		{
			if (_identityManager == null)
				throw new ConfigurationErrorsException("Identity manager is not created! Please call onBeginTransform() first");
			
			_identityManager.TablePrefix = delivery.Parameters[Consts.DeliveryHistoryParameters.TablePerfix].ToString();
			_identityManager.AccountId = delivery.Account.ID;

			// store timestamp of starting Transform for using it in Staging
			delivery.Parameters[Consts.DeliveryHistoryParameters.TransformTimestamp] = DateTime.Now;

			if (pass == TRANSFORM_PASS_IDENTITY)
			{
				// set identity of edge objects in Delivery according to existing objects in EdgeObject DB
				_identityManager.IdentifyDeliveryObjects();
			}
			else if (pass == TRANSFORM_PASS_CHECKSUM)
			{
				// CHECKSUMMANAGER: perform checksum validation
			}
			else if (pass == TRANSFORM_PASS_CURRENCY)
			{
				// CURRENCYMANAGER: call currency converter SP on metrics table
			}
		}

		/*=========================*/
		#endregion

		#region Staging
		/*=========================*/

		SqlTransaction _stageTransaction;
		const int STAGING_PASS_OBJECTS = 0;
		const int STAGING_PASS_METRICS = 1;

		protected override int StagePassCount
		{
			get { return 2; }
		}

		protected override void OnBeginStage()
		{
			if (String.IsNullOrWhiteSpace(Options.SqlStageCommand))
				throw new DeliveryManagerException("Options.SqlStageCommand is empty.");

			_identityManager = new IdentityManager(_deliverySqlConnection, _objectsSqlConnection);

			// TODO: handle transaction on objects?
			//_stageTransaction = _objectsSqlConnection.BeginTransaction("Delivery Staging");
		}

		protected override void OnStage(Delivery delivery, int pass)
		{
			CurrentDelivery = delivery;

			if (pass == STAGING_PASS_OBJECTS)
			{
				_identityManager.TablePrefix = delivery.Parameters[Consts.DeliveryHistoryParameters.TablePerfix].ToString();
				_identityManager.TransformTimestamp = DateTime.Parse(delivery.Parameters[Consts.DeliveryHistoryParameters.TransformTimestamp].ToString());
				_identityManager.AccountId = delivery.Account.ID;
				
				// IDENTITYMANAGER: insert new EdgeObjects and update existing from Delivery to EdgeObject DB by IdentityStatus
				_identityManager.UpdateEdgeObjects(_stageTransaction);
			}
			else if (pass == STAGING_PASS_METRICS)
			{
				// TABLEMANAGER: insert delivery metrics into staging
				_metricsTableManager.Stage(CurrentDelivery.Parameters[Consts.DeliveryHistoryParameters.StagingMetricsTableName].ToString(),
										   CurrentDelivery.Parameters[Consts.DeliveryHistoryParameters.DeliveryMetricsTableName].ToString());
			}
		}

		protected override void OnEndStage(Exception ex)
		{
			if (_stageTransaction != null)
			{
				if (ex == null)
					_stageTransaction.Commit();
				else
					_stageTransaction.Rollback();
			}
		}

		protected override void OnDisposeStage()
		{
			if (_stageTransaction != null)
				_stageTransaction.Dispose();
		}

		/*=========================*/
		#endregion

		#region Rollback
		/*=========================*/

		SqlCommand _rollbackCommand;
		SqlTransaction _rollbackTransaction;

		protected override void OnBeginRollback()
		{
			_rollbackTransaction = _deliverySqlConnection.BeginTransaction("Delivery Rollback");
		}

		protected override void OnRollbackDelivery(Delivery delivery, int pass)
		{
			string guid = delivery.DeliveryID.ToString("N");

			_rollbackCommand = _rollbackCommand ?? SqlUtility.CreateCommand(Options.SqlRollbackCommand, CommandType.StoredProcedure);
			_rollbackCommand.Connection = _deliverySqlConnection;
			_rollbackCommand.Transaction = _rollbackTransaction;

			_rollbackCommand.Parameters["@DeliveryID"].Value = guid;
			_rollbackCommand.Parameters["@StagingTable"].Value = CurrentDelivery.Parameters[Consts.DeliveryHistoryParameters.StagingMetricsTableName];

			_rollbackCommand.ExecuteNonQuery();

			// This is redundant (SP already does this) but to sync our objects in memory we do it here also
			foreach (DeliveryOutput output in delivery.Outputs)
				output.Status = DeliveryOutputStatus.RolledBack;
		}

		protected override void OnRollbackOutput(DeliveryOutput output, int pass)
		{
			string guid = output.OutputID.ToString("N");
			if (output.Status == DeliveryOutputStatus.Staged)
			{
				_rollbackCommand = _rollbackCommand ?? SqlUtility.CreateCommand(Options.SqlRollbackCommand, CommandType.StoredProcedure);
				_rollbackCommand.Connection = _deliverySqlConnection;
				_rollbackCommand.Transaction = _rollbackTransaction;

				_rollbackCommand.Parameters["@DeliveryOutputID"].Value = guid;
				_rollbackCommand.Parameters["@StagingTable"].Value = output.Parameters[Consts.DeliveryHistoryParameters.StagingMetricsTableName];

				_rollbackCommand.ExecuteNonQuery();
			}
			else if (output.Status == DeliveryOutputStatus.Committed)
			{
				output.Status = DeliveryOutputStatus.RollbackPending;
			}
			else
				throw new InvalidOperationException("Delivery output cannot be rolled back because it is neither staged nor committed.");
		}

		protected override void OnEndRollback(Exception ex)
		{
			if (ex == null)
				_rollbackTransaction.Commit();
			else
				_rollbackTransaction.Rollback();
		}

		protected override void OnDisposeRollback()
		{
			if (_rollbackTransaction != null)
				_rollbackTransaction.Dispose();
		}

		/*=========================*/
		#endregion

		#region Commin
		protected override void OnCommit(Delivery delivery, int pass)
		{
			throw new NotImplementedException();
		} 
		#endregion

		#region Misc
		/*=========================*/

		SqlConnection OpenDbConnection(string constConnection)
		{
			var connectionString = AppSettings.GetConnectionString(this, constConnection);
			var connection = new SqlConnection(connectionString);
			connection.Open();
			return connection;
		}

		protected override void OnDispose()
		{
			if (_deliverySqlConnection != null)
			{
				_deliverySqlConnection.Close();
				_deliverySqlConnection.Dispose();
			}

			if (_objectsSqlConnection != null)
			{
				_objectsSqlConnection.Close();
				_objectsSqlConnection.Dispose();
			}
		}

		/*=========================*/
		#endregion
	}
}
