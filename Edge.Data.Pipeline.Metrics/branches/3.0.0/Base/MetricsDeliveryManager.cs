using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using Edge.Core.Configuration;
using Edge.Core.Utilities;
using Edge.Data.Objects;
using Edge.Data.Pipeline.Metrics.Base.Submanagers;
using Edge.Data.Pipeline.Objects;

namespace Edge.Data.Pipeline.Metrics.Base
{
	/// <summary>
	/// Base class for metrics import managers.
	/// </summary>
	public abstract class MetricsDeliveryManager : DeliveryManager
	{
		#region Data Members
		private SqlConnection _sqlConnection;

		public Dictionary<string, Measure> Measures { get; private set; }
		public Dictionary<string, ConnectionDefinition> Connections { get; private set; }
		public MetricsDeliveryManagerOptions Options { get; private set; }
		#endregion

		#region Constructors
		/*=========================*/

		protected MetricsDeliveryManager(Guid serviceInstanceID, MetricsDeliveryManagerOptions options = null)
			: base(serviceInstanceID)
		{
			options = options ?? new MetricsDeliveryManagerOptions();
			options.StagingConnectionString = options.StagingConnectionString ?? AppSettings.GetConnectionString(this, Consts.ConnectionStrings.Staging);
			options.CommitConnectionString = options.CommitConnectionString ?? AppSettings.GetConnectionString(this, Consts.ConnectionStrings.DataWarehouse);
			options.SqlTransformCommand = options.SqlTransformCommand ?? AppSettings.Get(this, Consts.AppSettings.SqlTransformCommand, throwException: false);
			options.SqlStageCommand = options.SqlStageCommand ?? AppSettings.Get(this, Consts.AppSettings.SqlStageCommand, throwException: false);
			options.SqlCommitCommand = options.SqlStageCommand ?? AppSettings.Get(this, Consts.AppSettings.SqlCommitCommand, throwException: false);
			options.SqlRollbackCommand = options.SqlRollbackCommand ?? AppSettings.Get(this, Consts.AppSettings.SqlRollbackCommand, throwException: false);

			Options = options;
		}

		/*=========================*/
		#endregion

		#region Import
		/*=========================*/

		private string _tablePrefix;
		private TableManager _tableManager;

		protected override void OnBeginImport()
		{
			_tablePrefix = string.Format("{0}_{1}_{2}_{3}", CurrentDelivery.Account.ID, CurrentDelivery.Name, DateTime.Now.ToString("yyyMMdd_HHmmss"), CurrentDelivery.DeliveryID.ToString("N").ToLower());
			CurrentDelivery.Parameters.Add(Consts.DeliveryHistoryParameters.TablePerfix, _tablePrefix);

			//int bufferSize = int.Parse(AppSettings.Get(this, Consts.AppSettings.BufferSize));

			// MAPPER: load measures and properties using account/channel and options
			// this.Measures = 
			// this.MetaProperties = 
			LoadMeasures();

			// Connect to database
			_sqlConnection = NewDeliveryDbConnection();
			_sqlConnection.Open();

			// OBJECTMANAGER: run SP to setup delivery object tables (Usid instead of GK)
			// EXAMPLE - ObjectManager.CreateDeliveryObjectTables(string tablePrefix)

			// TABLEMANAGER: run SP to create metrics table

			// TODO shirat - to replace sample unit by metrics unit from mapping
			
			var exampleUnit = new GenericMetricsUnit
				{
					Account = new Account {ID = 1, Name = "Shira"},
					Channel = new Channel {ID = 1},
					TimePeriodStart = DateTime.Now,
					TimePeriodEnd = DateTime.Now,
					TargetDimensions = new List<TargetMatch>(),
					MeasureValues = new Dictionary<Measure, double>
						{
							{new Measure {Name = "Application"}, 0.00},
							{new Measure {Name = "Account"}, 0.00}
						},
				};

			//var exampleUnit = new AdMetricsUnit {Ad = new Ad {Creative = new TextCreative()}};
			//exampleUnit.TargetDimensions = new List<TargetMatch>();
			//var targetMatch = new TargetMatch {Target = new KeywordTarget(), TargetDefinition = new TargetDefinition {Target = new KeywordTarget()}};
			//exampleUnit.TargetDimensions.Add(targetMatch);
			//exampleUnit.MeasureValues = new Dictionary<Measure, double>
			//	{
			//		{new Measure {Name = "Measure1"}, 12.00},
			//		{new Measure {Name = "Measure2"}, 23.00}
			//	};


			_tableManager = new TableManager(_sqlConnection);
			string tableName = _tableManager.CreateDeliveryMetricsTable(_tablePrefix,exampleUnit);
			CurrentDelivery.Parameters[Consts.DeliveryHistoryParameters.DeliveryMetricsTableName] = tableName;

			// CHECKSUMMANAGER: setup

			// MAPPER: setup bulks for objects and metrics
		}

		public virtual void ImportMetrics(DeliveryOutput targetOutput, MetricsUnit metrics)
		{
			EnsureBeginImport();

			//foreach (EdgeObject obj in metrics.GetObjectDimensions())
			//{

			//}
		}

		protected override void OnEndImport()
		{
			// MAPPER: flush all the bulks
		}

		protected void EnsureBeginImport()
		{
			if (State != DeliveryManagerState.Importing)
				throw new InvalidOperationException("BeginImport must be called before anything can be imported.");
		}

		/// <summary>
		/// Load meatures from DB by account and channel
		/// </summary>
		private void LoadMeasures()
		{
			Measures = new Dictionary<string, Measure>();
		}
		/*=========================*/

		#endregion

		#region Prepare
		/*=========================*/

		//SqlCommand _transformCommand = null;
		//SqlCommand _validateCommand = null;
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

			_sqlConnection = NewDeliveryDbConnection();
			_sqlConnection.Open();
		}

		protected override void OnTransform(Delivery delivery, int pass)
		{
			//string tablePerfix = (string)delivery.Parameters[Consts.DeliveryHistoryParameters.TablePerfix];

			if (pass == TRANSFORM_PASS_IDENTITY)
			{
				// OBJECTMANAGER: call identity SP - setup GK cache and assign existing GKs to object tables
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
		//SqlCommand _stageCommand = null;
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

			_sqlConnection = NewDeliveryDbConnection();
			_sqlConnection.Open();

			_stageTransaction = _sqlConnection.BeginTransaction("Delivery Staging");
		}

		protected override void OnStage(Delivery delivery, int pass)
		{
			//string tablePerfix = (string)delivery.Parameters[Consts.DeliveryHistoryParameters.TablePerfix];
			//string deliveryId = delivery.DeliveryID.ToString("N");

			if (pass == STAGING_PASS_OBJECTS)
			{
				// OBJECTMANAGER: call object insert SP with identity manager GK creation
			}
			else if (pass == STAGING_PASS_METRICS)
			{
				// TABLEMANAGER: find matching staging table and save to delivery parameter
				string stagingMetricsTableName=_tableManager.FindStagingTable(CurrentDelivery.Parameters[Consts.DeliveryHistoryParameters.DeliveryMetricsTableName].ToString());
				CurrentDelivery.Parameters[Consts.DeliveryHistoryParameters.StagingMetricsTableName] = stagingMetricsTableName;
				
				// TABLEMANAGER: call metrics insert SP with identity manager USID --> GK translation
				_tableManager.Staging(CurrentDelivery.Parameters[Consts.DeliveryHistoryParameters.StagingMetricsTableName].ToString(),
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
			_sqlConnection = NewDeliveryDbConnection();
			_sqlConnection.Open();
			_rollbackTransaction = _sqlConnection.BeginTransaction("Delivery Rollback");
		}

		protected override void OnRollbackDelivery(Delivery delivery, int pass)
		{
			string guid = delivery.DeliveryID.ToString("N");

			_rollbackCommand = _rollbackCommand ?? SqlUtility.CreateCommand(Options.SqlRollbackCommand, CommandType.StoredProcedure);
			_rollbackCommand.Connection = _sqlConnection;
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
			var connectionString = AppSettings.GetConnectionString(this, Consts.ConnectionStrings.Deliveries);
			return new SqlConnection(connectionString);
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
	/// A type-safe base class for metrics import managers
	/// </summary>
	/// <typeparam name="TMetricsUnit"></typeparam>
	public abstract class MetricsDeliveryManager<TMetricsUnit> : MetricsDeliveryManager where TMetricsUnit : MetricsUnit
	{
		protected MetricsDeliveryManager(Guid serviceInstanceID, MetricsDeliveryManagerOptions options = null)
			: base(serviceInstanceID, options)
		{
		}

		public override void ImportMetrics(DeliveryOutput targetOutput, MetricsUnit metrics)
		{
			ImportMetrics((TMetricsUnit)metrics);
		}

		public abstract void ImportMetrics(TMetricsUnit metrics);
	}
}
