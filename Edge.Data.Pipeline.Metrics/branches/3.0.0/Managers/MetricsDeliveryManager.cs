using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using Edge.Core.Configuration;
using Edge.Core.Utilities;
using Edge.Data.Objects;
using Edge.Data.Pipeline.Metrics.Indentity;
using Edge.Data.Pipeline.Metrics.Misc;
using Edge.Data.Pipeline.Objects;
using LogMessageType = Edge.Core.Utilities.LogMessageType;

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

		private string _tablePrefix;
		private readonly MetricsTableManager _metricsTableManager;
		private readonly EdgeObjectsManager _edgeObjectsManager;

		public MetricsDeliveryManagerOptions Options { get; private set; }
		public Action<string, LogMessageType> OnLog { get; set; }

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
			_objectsSqlConnection  = OpenDbConnection(Consts.ConnectionStrings.Objects);

			_edgeObjectsManager = new EdgeObjectsManager(_deliverySqlConnection, _objectsSqlConnection) {EdgeTypes = edgeTypes};
			_metricsTableManager = new MetricsTableManager(_deliverySqlConnection, _edgeObjectsManager) { EdgeTypes = edgeTypes };
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

			Log(String.Format("Delivery object tables created for delivery {0}", CurrentDelivery.DeliveryID));
			
			// create metrics table using metrics table manager and sample metrics
			_metricsTableManager.CreateDeliveryMetricsTable(_tablePrefix, sampleMetrics);

			Log(String.Format("Delivery Metrics table '{0}' created for delivery {1}", _metricsTableManager.TableName, CurrentDelivery.DeliveryID));

			// store delivery and staging (best match) table names in delivery
			CurrentDelivery.Parameters[Consts.DeliveryHistoryParameters.DeliveryMetricsTableName] = _metricsTableManager.TableName;
			CurrentDelivery.Parameters[Consts.DeliveryHistoryParameters.StagingMetricsTableName] = _metricsTableManager.FindStagingTable();

			Log(String.Format("Best match metrics table is '{0}' for delivery {1}", CurrentDelivery.Parameters[Consts.DeliveryHistoryParameters.StagingMetricsTableName], CurrentDelivery.DeliveryID));

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

			foreach(var output in CurrentDelivery.Outputs)
				output.Status = DeliveryOutputStatus.Imported;
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
		}

		protected override void OnTransform(Delivery delivery, int pass)
		{
			if (pass == TRANSFORM_PASS_IDENTITY)
			{
				// store timestamp of starting Transform for using it in Staging
				delivery.Parameters[Consts.DeliveryHistoryParameters.TransformTimestamp] = DateTime.Now;
				
				// set identity of edge objects in Delivery according to existing objects in EdgeObject DB
				Identify(1, delivery);

				foreach (var output in delivery.Outputs)
					output.Status = DeliveryOutputStatus.Transformed;
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
			// TODO: handle transaction on objects?
			//_stageTransaction = _objectsSqlConnection.BeginTransaction("Delivery Staging");
		}

		protected override void OnStage(Delivery delivery, int pass)
		{
			CurrentDelivery = delivery;

			if (pass == STAGING_PASS_OBJECTS)
			{
				// IDENTITYMANAGER: insert new EdgeObjects and update existing from Delivery to EdgeObject DB by IdentityStatus
				Identify(2, delivery);
			}
			else if (pass == STAGING_PASS_METRICS)
			{
				// TABLEMANAGER: insert delivery metrics into staging
				if (CurrentDelivery.Parameters[Consts.DeliveryHistoryParameters.StagingMetricsTableName] != null &&
					CurrentDelivery.Parameters[Consts.DeliveryHistoryParameters.DeliveryMetricsTableName] != null)
				{
					_metricsTableManager.Stage(delivery.Account.ID, CurrentDelivery.Parameters[Consts.DeliveryHistoryParameters.DeliveryMetricsTableName].ToString(),
											   CurrentDelivery.Parameters[Consts.DeliveryHistoryParameters.StagingMetricsTableName].ToString());
				}
			}
		}

		protected override void OnEndStage(Exception ex)
		{
			if (_stageTransaction != null)
			{
				if (ex == null)
				{
					_stageTransaction.Commit();
					foreach (var output in CurrentDelivery.Outputs)
						output.Status = DeliveryOutputStatus.Staged;
				}
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

		/// <summary>
		/// Help function to execute Identity stages:
		/// stage 1 - identify delivery object by existing Edge Objects
		/// stage 2 - insert new and update odified Edge Objects by delivery objects
		/// DEBUG (Options): execute Identity Manager .NET code
		/// REAL: execute SQl CLR wich executes Identity Manager .NET code in DB
		/// </summary>
		protected void Identify(int identityStage, Delivery delivery)
		{
			// for Debug only - execute Identity Manager in .NET
			if (Options.IdentityInDebug)
			{
				var identityManager = new IdentityManager(_objectsSqlConnection)
				{
					TablePrefix = delivery.Parameters[Consts.DeliveryHistoryParameters.TablePerfix].ToString(),
					TransformTimestamp = DateTime.Parse(delivery.Parameters[Consts.DeliveryHistoryParameters.TransformTimestamp].ToString()),
					AccountId = delivery.Account.ID, 
					CreateNewEdgeObjects = Options.CreateNewEdgeObjects
				};

				if (identityStage == 1) identityManager.IdentifyDeliveryObjects();
				else if (identityStage == 2) identityManager.UpdateEdgeObjects();
			}
			// to be executed in real scenario - execute SQL CLR (DB stored procedure that executes .NET code of Identity Manager)
			else
			{
				var spName = identityStage == 1 ? "EdgeObjects.dbo.IdentityI" : "EdgeObjects.dbo.IdentityII";
				using (var cmd = SqlUtility.CreateCommand(spName, CommandType.StoredProcedure))
				{
					cmd.Connection = _objectsSqlConnection;
					cmd.Parameters.AddWithValue("@accoutId", delivery.Account.ID);
					cmd.Parameters.AddWithValue("@deliveryTablePrefix", delivery.Parameters[Consts.DeliveryHistoryParameters.TablePerfix].ToString());
					cmd.Parameters.AddWithValue("@identity1Timestamp", DateTime.Parse(delivery.Parameters[Consts.DeliveryHistoryParameters.TransformTimestamp].ToString()));
					cmd.Parameters.AddWithValue("@createNewEdgeObjects", Options.CreateNewEdgeObjects);
					
					cmd.ExecuteNonQuery();
				}
			}
		}

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

		protected void Log(string msg, LogMessageType logType = LogMessageType.Debug)
		{
			if (OnLog != null) OnLog(msg, logType);
		}

		/*=========================*/
		#endregion
	}
}
