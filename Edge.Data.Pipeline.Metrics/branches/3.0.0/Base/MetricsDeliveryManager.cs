using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using Edge.Core.Configuration;
using Edge.Core.Utilities;
using Edge.Data.Objects;
using Edge.Core.Services;

namespace Edge.Data.Pipeline.Metrics
{
	/// <summary>
	/// Base class for metrics import managers.
	/// </summary>
	public abstract class MetricsDeliveryManager : DeliveryManager
	{
		#region Fields
		/*=========================*/

		private SqlConnection _sqlConnection;

		public Dictionary<string, Measure> Measures { get; private set; }
		public Dictionary<string, MetaProperty> MetaProperties { get; private set; }
		public MetricsDeliveryManagerOptions Options { get; private set; }
		public string DeliveryName { get; private set; }

		/*=========================*/
		#endregion

		#region Constructors
		/*=========================*/

		public MetricsDeliveryManager(long serviceInstanceID, string deliveryName, MetricsDeliveryManagerOptions options = null)
			: base(serviceInstanceID)
		{
			options = options ?? new MetricsDeliveryManagerOptions();
			options.StagingConnectionString = options.StagingConnectionString ?? AppSettings.GetConnectionString(this, Consts.ConnectionStrings.Staging);
			options.CommitConnectionString = options.CommitConnectionString ?? AppSettings.GetConnectionString(this, Consts.ConnectionStrings.DataWarehouse);
			options.SqlTransformCommand = options.SqlTransformCommand ?? AppSettings.Get(this, Consts.AppSettings.SqlTransformCommand, throwException: false);
			options.SqlStageCommand = options.SqlStageCommand ?? AppSettings.Get(this, Consts.AppSettings.SqlStageCommand, throwException: false);
			options.SqlCommitCommand = options.SqlStageCommand ?? AppSettings.Get(this, Consts.AppSettings.SqlCommitCommand, throwException: false);
			options.SqlRollbackCommand = options.SqlRollbackCommand ?? AppSettings.Get(this, Consts.AppSettings.SqlRollbackCommand, throwException: false);

			this.Options = options;
			this.DeliveryName = deliveryName;
		}

		/*=========================*/
		#endregion

		#region Import
		/*=========================*/

		private string _tablePrefix;

		protected override void OnBeginImport()
		{
			this._tablePrefix = string.Format("{0}_{1}_{2}_{3}", this.CurrentDelivery.Account.ID, this.DeliveryName, DateTime.Now.ToString("yyyMMdd_HHmmss"), this.CurrentDelivery.DeliveryID.ToString("N").ToLower());
			this.CurrentDelivery.Parameters.Add(Consts.DeliveryHistoryParameters.TablePerfix, this._tablePrefix);

			int bufferSize = int.Parse(AppSettings.Get(this, Consts.AppSettings.BufferSize));

			// MAPPER: load measures and properties using account/channel and options
			// this.Measures = 
			// this.MetaProperties = 

			// Connect to database
			_sqlConnection = NewDeliveryDbConnection();
			_sqlConnection.Open();

			// OBJECTMANAGER: run SP to setup delivery object tables (Usid instead of GK)
			// EXAMPLE - ObjectManager.CreateDeliveryObjectTables(string tablePrefix)

			// TABLEMANAGER: run SP to create metrics table
			// EXAMPLE - TableManager.CreateDeliveryMetricsTable(string tablePrefix, MetricsUnit exampleUnit) ad_gk ad_usid!!!!! foreach edge object
			//this.CurrentDelivery.Parameters[Consts.DeliveryHistoryParameters.DeliveryMetricsTableName] = 

			// CHECKSUMMANAGER: setup

			// MAPPER: setup bulks for objects and metrics
		}

		public abstract void ImportObject(EdgeObject edgeObject);
		public abstract void ImportMetrics(MetricsUnit metrics);

		protected override void OnEndImport()
		{
			// MAPPER: flush all the bulks
		}

		protected void EnsureBeginImport()
		{
			if (this.State != DeliveryManagerState.Importing)
				throw new InvalidOperationException("BeginImport must be called before anything can be imported.");
		}

		/*=========================*/
		#endregion

		#region Prepare
		/*=========================*/

		SqlCommand _transformCommand = null;
		SqlCommand _validateCommand = null;
		const int TransformPass_Identity = 0;
		const int TransformPass_Checksum = 1;
		const int TransformPass_Currency = 2;

		protected override int TransformPassCount
		{
			get { return 3; }
		}

		protected override void OnBeginTransform()
		{
			if (String.IsNullOrWhiteSpace(this.Options.SqlTransformCommand))
				throw new DeliveryManagerException("Options.SqlTransformCommand is empty.");

			_sqlConnection = NewDeliveryDbConnection();
			_sqlConnection.Open();
		}

		protected override void OnTransform(Delivery delivery, int pass)
		{
			string tablePerfix = (string)delivery.Parameters[Consts.DeliveryHistoryParameters.TablePerfix];

			if (pass == TransformPass_Identity)
			{
				// OBJECTMANAGER: call identity SP - setup GK cache and assign existing GKs to object tables
			}
			else if (pass == TransformPass_Checksum)
			{
				// CHECKSUMMANAGER: perform checksum validation
			}
			else if (pass == TransformPass_Currency)
			{
				// CURRENCYMANAGER: call currency converter SP on metrics table
			}
		}

		/*=========================*/
		#endregion

		#region Staging
		/*=========================*/

		SqlTransaction _stageTransaction = null;
		SqlCommand _stageCommand = null;
		const int StagingPass_Objects = 0;
		const int StagingPass_Metrics = 1;

		protected override int StagePassCount
		{
			get { return 2; }
		}

		protected override void OnBeginStage()
		{
			if (String.IsNullOrWhiteSpace(this.Options.SqlStageCommand))
				throw new DeliveryManagerException("Options.SqlStageCommand is empty.");

			_sqlConnection = NewDeliveryDbConnection();
			_sqlConnection.Open();

			_stageTransaction = _sqlConnection.BeginTransaction("Delivery Staging");
		}

		protected override void OnStage(Delivery delivery, int pass)
		{
			string tablePerfix = (string)delivery.Parameters[Consts.DeliveryHistoryParameters.TablePerfix];
			string deliveryId = delivery.DeliveryID.ToString("N");

			if (pass == StagingPass_Objects)
			{
				// OBJECTMANAGER: call object insert SP with identity manager GK creation
			}
			else if (pass == StagingPass_Metrics)
			{
				// TABLEMANAGER: find matching staging table and save to delivery parameter
				// this.CurrentDelivery.Parameters[Consts.DeliveryHistoryParameters.StagingMetricsTableName] = TableManager.FindStagingTable(this.CurrentDelivery.Parameters[Consts.DeliveryHistoryParameters.DeliveryMetricsTableName]);
				
				// TABLEMANAGER: call metrics insert SP with identity manager USID --> GK translation
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

		SqlCommand _rollbackCommand = null;
		SqlTransaction _rollbackTransaction = null;

		protected override void OnBeginRollback()
		{
			_sqlConnection = NewDeliveryDbConnection();
			_sqlConnection.Open();
			_rollbackTransaction = _sqlConnection.BeginTransaction("Delivery Rollback");
		}

		protected override void OnRollbackDelivery(Delivery delivery, int pass)
		{
			string guid = delivery.DeliveryID.ToString("N");

			_rollbackCommand = _rollbackCommand ?? SqlUtility.CreateCommand(this.Options.SqlRollbackCommand, CommandType.StoredProcedure);
			_rollbackCommand.Connection = _sqlConnection;
			_rollbackCommand.Transaction = _rollbackTransaction;

			_rollbackCommand.Parameters["@DeliveryID"].Value = guid;
			_rollbackCommand.Parameters["@StagingTable"].Value = this.CurrentDelivery.Parameters[Consts.DeliveryHistoryParameters.StagingMetricsTableName];

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
				_rollbackCommand = _rollbackCommand ?? SqlUtility.CreateCommand(this.Options.SqlRollbackCommand, CommandType.StoredProcedure);
				_rollbackCommand.Connection = _sqlConnection;
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

		#region Misc
		/*=========================*/

		SqlConnection NewDeliveryDbConnection()
		{
			return new SqlConnection(AppSettings.GetConnectionString(Consts.ConnectionStrings.Deliveries));
		}

		protected override void OnDispose()
		{
			if (_sqlConnection != null)
				_sqlConnection.Dispose();
		}

		/*=========================*/
		#endregion


	}

	/// <summary>
	/// A type-safe base class for metrics import managers.
	/// </summary>
	/// <typeparam name="MetricsUnitT"></typeparam>
	public abstract class MetricsDeliveryManager<MetricsUnitT> : MetricsDeliveryManager where MetricsUnitT : MetricsUnit
	{
		public MetricsDeliveryManager(long serviceInstanceID, MetricsDeliveryManagerOptions options = null)
			: base(serviceInstanceID, options)
		{
		}

		public override void ImportMetrics(MetricsUnit metrics)
		{
			this.ImportMetrics((MetricsUnitT)metrics);
		}

		public abstract void ImportMetrics(MetricsUnitT metrics);
	}
}
