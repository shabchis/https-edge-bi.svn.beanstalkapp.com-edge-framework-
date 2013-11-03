using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using Edge.Core.Configuration;
using Edge.Core.Data;
using Edge.Core.Utilities;
using Edge.Data.Objects;
using Edge.Data.Pipeline.Common.Importing;

namespace Edge.Data.Pipeline.Metrics
{
	/// <summary>
	/// Base class for metrics import managers.
	/// </summary>
	public abstract class MetricsImportManager : DeliveryImportManager
	{
		#region Fields
		/*=========================*/

		private SqlConnection _sqlConnection;

		public Dictionary<string, Measure> Measures { get; private set; }
		public Dictionary<string, Segment> SegmentTypes { get; private set; }
		public MetricsImportManagerOptions Options { get; private set; }
        public List<CurrencyRate> CurrencyRates { get; private set; }

		/*=========================*/
		#endregion

		#region Constructors
		/*=========================*/

		public MetricsImportManager(long serviceInstanceID, MetricsImportManagerOptions options = null)
			: base(serviceInstanceID)
		{
			options = options ?? new MetricsImportManagerOptions();
			options.StagingConnectionString = options.StagingConnectionString ?? AppSettings.GetConnectionString(this, Consts.ConnectionStrings.StagingDatabase);
			options.SqlTransformCommand = options.SqlTransformCommand ?? AppSettings.Get(this, Consts.AppSettings.SqlTransformCommand, throwException: false);
			options.SqlStageCommand = options.SqlStageCommand ?? AppSettings.Get(this, Consts.AppSettings.SqlStageCommand, throwException: false);
			options.SqlRollbackCommand = options.SqlRollbackCommand ?? AppSettings.Get(this, Consts.AppSettings.SqlRollbackCommand, throwException: false);

			this.Options = options;
		}

		/*=========================*/
		#endregion

		#region Import
		/*=========================*/

		private string _tablePrefix;
		private Dictionary<Type, BulkObjects> _bulks;

		protected override void OnBeginImport()
		{
			this._tablePrefix = string.Format("{0}_{1}_{2}_{3}", this.TablePrefixType, this.CurrentDelivery.Account.ID, DateTime.Now.ToString("yyyMMdd_HHmmss"), this.CurrentDelivery.DeliveryID.ToString("N").ToLower());
			this.CurrentDelivery.Parameters[Consts.DeliveryOutputParameters.TablePerfix] = this._tablePrefix;

			int bufferSize = int.Parse(AppSettings.Get(this, Consts.AppSettings.BufferSize));

			// Connect to database
			_sqlConnection = NewDeliveryDbConnection();
			_sqlConnection.Open();

			// Create bulk objects
			_bulks = new Dictionary<Type, BulkObjects>();
			Type tableList = this.GetType().GetNestedType("Tables", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
			foreach (Type table in tableList.GetNestedTypes())
			{
				_bulks[table] = new BulkObjects(this._tablePrefix, table, _sqlConnection, bufferSize);
			}

			// Get measures
			using (SqlConnection oltpConnection = new SqlConnection(this.Options.StagingConnectionString))
			{
				oltpConnection.Open();

				this.Measures = Measure.GetMeasures(
					this.CurrentDelivery.Account,
					this.CurrentDelivery.Channel,
					oltpConnection,
					this.Options.MeasureOptions,
					this.Options.MeasureOptionsOperator
					);

				this.SegmentTypes = Segment.GetSegments(
					this.CurrentDelivery.Account,
					this.CurrentDelivery.Channel,
					oltpConnection,
					this.Options.SegmentOptions,
					this.Options.SegmentOptionsOperator
					);

                //this.CurrencyRates = CurrencyRate.GetCurrencyRate(
                //    oltpConnection,
                //    this.CurrentDelivery.TimePeriodStart
                //    );
			}

			// Add measure columns to metrics
			StringBuilder measuresFieldNamesSQL = new StringBuilder(",");
			StringBuilder measuresNamesSQL = new StringBuilder(",");
			StringBuilder measuresValidationSQL = new StringBuilder();
			int count = 0;
			BulkObjects bulkMetrics = _bulks[this.MetricsTableDefinition];
			foreach (Measure measure in this.Measures.Values)
			{
				bulkMetrics.AddColumn(new ColumnDef(
					name: measure.Name,
					type: SqlDbType.Float,
					nullable: true
					));

				measuresFieldNamesSQL.AppendFormat("[{0}]{1}", measure.OltpName, count < this.Measures.Values.Count - 1 ? "," : null);
				measuresNamesSQL.AppendFormat("[{0}]{1}", measure.Name, count < this.Measures.Values.Count - 1 ? "," : null);

				if (measure.Options.HasFlag(MeasureOptions.ValidationRequired))
					measuresValidationSQL.AppendFormat("{1}SUM([{0}]) as [{0}]", measure.Name, measuresValidationSQL.Length > 0 ? ", " : null);

				count++;
			}

			this.CurrentDelivery.Parameters.Add(Consts.DeliveryOutputParameters.MeasureFieldsSql, measuresFieldNamesSQL.ToString());
			this.CurrentDelivery.Parameters.Add(Consts.DeliveryOutputParameters.MeasureNamesSql, measuresNamesSQL.ToString());
			if (string.IsNullOrEmpty(measuresValidationSQL.ToString()))
				Log.Write("No measures marked for checksum validation; there will be no validation before the final commit.", LogMessageType.Warning);
			else
				this.CurrentDelivery.Parameters.Add(Consts.DeliveryOutputParameters.MeasureValidateSql, measuresValidationSQL.ToString());

			// Create the tables
			StringBuilder createTableCmdText = new StringBuilder();
			foreach (BulkObjects bulk in _bulks.Values)
				createTableCmdText.Append(bulk.GetCreateTableSql());
			SqlCommand cmd = new SqlCommand(createTableCmdText.ToString(), _sqlConnection);
			cmd.CommandTimeout = 80; //DEFAULT IS 30 AND SOMTIME NOT ENOUGH WHEN RUNING CUBE
			cmd.ExecuteNonQuery();
		}

		public abstract void ImportMetrics(MetricsUnit metrics);

		protected override void OnEndImport()
		{
			foreach (BulkObjects bulk in _bulks.Values)
			{
				bulk.Flush();
				bulk.Dispose();
			}
		}

		protected BulkObjects Bulk<TableDef>()
		{
			return _bulks[typeof(TableDef)];
		}

		protected void EnsureBeginImport()
		{
			if (this.State != DeliveryImportManagerState.Importing)
				throw new InvalidOperationException("BeginImport must be called before anything can be imported.");
		}

		protected abstract string TablePrefixType { get; }
		protected abstract Type MetricsTableDefinition { get; }


		/*=========================*/
		#endregion

		#region Prepare
		/*=========================*/

		SqlCommand _transformCommand = null;
		SqlCommand _validateCommand = null;
		const int Transform_TRANSFORM_PASS = 0;
		const int Transform_VALIDATE_PASS = 1;
		const string ValidationTable = "Commit_FinalMetrics";

		protected override int TransformPassCount
		{
			get { return 2; }
		}

		protected override void OnBeginTransform()
		{
			if (String.IsNullOrWhiteSpace(this.Options.SqlTransformCommand))
				throw new ConfigurationException("Options.SqlPrepareCommand is empty.");

			_sqlConnection = NewDeliveryDbConnection();
			_sqlConnection.Open();
		}

		protected override void OnTransform(Delivery delivery, int pass)
		{


			// get this from last 'Processed' history entry
			string measuresFieldNamesSQL = delivery.Parameters[Consts.DeliveryOutputParameters.MeasureFieldsSql].ToString();
			string measuresNamesSQL = delivery.Parameters[Consts.DeliveryOutputParameters.MeasureNamesSql].ToString();

			string tablePerfix = delivery.Parameters[Consts.DeliveryOutputParameters.TablePerfix].ToString();
			string deliveryId = delivery.DeliveryID.ToString("N");

			if (pass == Transform_TRANSFORM_PASS)
			{
				// ...........................
				// PREPARE data

				_transformCommand = _transformCommand ?? DataManager.CreateCommand(this.Options.SqlTransformCommand, CommandType.StoredProcedure);
				_transformCommand.Connection = _sqlConnection;
				_transformCommand.Parameters["@DeliveryID"].Size = 4000;
				_transformCommand.Parameters["@DeliveryID"].Value = deliveryId;
				_transformCommand.Parameters["@DeliveryTablePrefix"].Size = 4000;
				_transformCommand.Parameters["@DeliveryTablePrefix"].Value = tablePerfix;
				_transformCommand.Parameters["@MeasuresNamesSQL"].Size = 4000;
				_transformCommand.Parameters["@MeasuresNamesSQL"].Value = measuresNamesSQL;
				_transformCommand.Parameters["@MeasuresFieldNamesSQL"].Size = 4000;
				_transformCommand.Parameters["@MeasuresFieldNamesSQL"].Value = measuresFieldNamesSQL;
				_transformCommand.Parameters["@CommitTableName"].Size = 4000;
				_transformCommand.Parameters["@CommitTableName"].Direction = ParameterDirection.Output;

				try { _transformCommand.ExecuteNonQuery(); }
				catch (Exception ex)
				{
					throw new Exception(String.Format("Delivery {0} failed during Transform.", deliveryId), ex);
				}

				delivery.Parameters[Consts.DeliveryOutputParameters.CommitTableName] = _transformCommand.Parameters["@CommitTableName"].Value;
				foreach (var output in delivery.Outputs)
					output.Parameters[Consts.DeliveryOutputParameters.CommitTableName] = _transformCommand.Parameters["@CommitTableName"].Value;
			}
			else if (pass == Transform_VALIDATE_PASS)
			{

				foreach (DeliveryOutput outPut in delivery.Outputs)
				{


					if (outPut.Checksum != null && outPut.Checksum.Count > 0)
					{


						object sql;
						if (delivery.Parameters.TryGetValue(Consts.DeliveryOutputParameters.MeasureValidateSql, out sql))
						{

							string measuresValidateSQL = (string)sql;
							measuresValidateSQL = measuresValidateSQL.Insert(0, "SELECT ");
							measuresValidateSQL = measuresValidateSQL + string.Format("\nFROM {0}_{1} \nWHERE outputID=@outputid:Nvarchar", tablePerfix, ValidationTable);

							SqlCommand validateCommand = DataManager.CreateCommand(measuresValidateSQL);
							validateCommand.Connection = _sqlConnection;
							validateCommand.Parameters["@outputid"].Value = outPut.OutputID.ToString("N");
							using (SqlDataReader reader = validateCommand.ExecuteReader())
							{
								if (reader.Read())
								{
									var results = new StringBuilder();
									foreach (KeyValuePair<string, double> total in outPut.Checksum)
									{
										if (reader[total.Key] is DBNull)
										{

											if (total.Value == 0)
												Log.Write(string.Format("[zero totals] {0} has no data or total is 0 in table {1} for target period {2}-{3}", total.Key, ValidationTable, outPut.TimePeriodStart, outPut.TimePeriodEnd), LogMessageType.Information);
											else
												results.AppendFormat("{0} is null in table {1}\n but {2} in measure {3}", total.Key, ValidationTable, total.Key, total.Value);
										}
										else
										{
											double val = Convert.ToDouble(reader[total.Key]);
											double diff = Math.Abs((total.Value - val) / total.Value);
											if (diff > this.Options.ChecksumThreshold)
												results.AppendFormat("{0}: processor totals = {1}, {2} table = {3}\n", total.Key, total.Value, ValidationTable, val);
											else if (val == 0 && total.Value == 0)
												Log.Write(string.Format("[zero totals] {0} has no data or total is 0 in table {1} for target period {2}-{3}", total.Key, ValidationTable, outPut.TimePeriodStart, outPut.TimePeriodEnd), LogMessageType.Information);


										}
									}
									if (results.Length > 0)
										throw new Exception("Commit validation (checksum) failed:\n" + results.ToString());
								}
								else
									throw new Exception(String.Format("Commit validation (checksum) did not find any data matching this delivery in {0}.", ValidationTable));
							}
						}

					}
				}
			}
		}

		/*=========================*/
		#endregion

		#region Staging
		/*=========================*/

		SqlTransaction _stageTransaction = null;
		SqlCommand _stageCommand = null;

		protected override int StagePassCount
		{
			get { return 1; }
		}

		protected override void OnBeginStage()
		{
			if (String.IsNullOrWhiteSpace(this.Options.SqlStageCommand))
				throw new ConfigurationException("Options.SqlStageCommand is empty.");

			_sqlConnection = NewDeliveryDbConnection();
			_sqlConnection.Open();

			_stageTransaction = _sqlConnection.BeginTransaction("Delivery Staging");
		}

		protected override void OnStage(Delivery delivery, int pass)
		{
			// get this from last 'Processed' history entry
			string measuresFieldNamesSQL = delivery.Parameters[Consts.DeliveryOutputParameters.MeasureFieldsSql].ToString();
			string measuresNamesSQL = delivery.Parameters[Consts.DeliveryOutputParameters.MeasureNamesSql].ToString();

			string tablePerfix = delivery.Parameters[Consts.DeliveryOutputParameters.TablePerfix].ToString();
			string deliveryId = delivery.DeliveryID.ToString("N");


			// ...........................
			// COMMIT data to OLTP

			_stageCommand = _stageCommand ?? DataManager.CreateCommand(this.Options.SqlStageCommand, CommandType.StoredProcedure);
			_stageCommand.Connection = _sqlConnection;
			_stageCommand.Transaction = _stageTransaction;

			_stageCommand.Parameters["@DeliveryFileName"].Size = 4000;
			_stageCommand.Parameters["@DeliveryFileName"].Value = tablePerfix;
			_stageCommand.Parameters["@CommitTableName"].Size = 4000;
			_stageCommand.Parameters["@CommitTableName"].Value = delivery.Parameters["CommitTableName"];
			_stageCommand.Parameters["@MeasuresNamesSQL"].Size = 4000;
			_stageCommand.Parameters["@MeasuresNamesSQL"].Value = measuresNamesSQL;
			_stageCommand.Parameters["@MeasuresFieldNamesSQL"].Size = 4000;
			_stageCommand.Parameters["@MeasuresFieldNamesSQL"].Value = measuresFieldNamesSQL;
			_stageCommand.Parameters["@OutputIDsPerSignature"].Size = 8000;
			_stageCommand.Parameters["@OutputIDsPerSignature"].Direction = ParameterDirection.Output;
			_stageCommand.Parameters["@DeliveryID"].Size = 4000;
			_stageCommand.Parameters["@DeliveryID"].Value = deliveryId;


			try
			{
				_stageCommand.ExecuteNonQuery();
				//	_commitTransaction.Commit();

				string outPutsIDsPerSignature = _stageCommand.Parameters["@OutputIDsPerSignature"].Value.ToString();

				string[] existsOutPuts;
				if ((!string.IsNullOrEmpty(outPutsIDsPerSignature) && outPutsIDsPerSignature != "0"))
				{
					_stageTransaction.Rollback();
					existsOutPuts = outPutsIDsPerSignature.Split(',');
					List<DeliveryOutput> outputs = new List<DeliveryOutput>();
					foreach (string existOutput in existsOutPuts)
					{					
							DeliveryOutput o = DeliveryOutput.Get(Guid.Parse(existOutput));
							o.Parameters[Consts.DeliveryOutputParameters.CommitTableName] = delivery.Parameters["CommitTableName"];
							outputs.Add(o);
					}
					throw new DeliveryConflictException(string.Format("DeliveryOutputs with the same signature are already committed in the database\n Deliveries:\n {0}:", outPutsIDsPerSignature)) { ConflictingOutputs = outputs.ToArray() };
				}
				else
					//already updated by sp, this is so we don't override it
					foreach (var output in delivery.Outputs)
					{
						output.Status = DeliveryOutputStatus.Staged;

					}
			}
			finally
			{
				this.State = DeliveryImportManagerState.Idle;

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
			this.State = DeliveryImportManagerState.Idle;

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

			_rollbackCommand = _rollbackCommand ?? DataManager.CreateCommand(this.Options.SqlRollbackCommand, CommandType.StoredProcedure);
			_rollbackCommand.Connection = _sqlConnection;
			_rollbackCommand.Transaction = _rollbackTransaction;

			_rollbackCommand.Parameters["@DeliveryID"].Value = guid;
			_rollbackCommand.Parameters["@TableName"].Value = this.CurrentDelivery.Parameters[Consts.DeliveryOutputParameters.CommitTableName];

			_rollbackCommand.ExecuteNonQuery();

			// This is redundant (SP already does this) but to sync our objects in memory we do it here also
			foreach (DeliveryOutput output in delivery.Outputs)
				output.Status = DeliveryOutputStatus.RolledBack;
		}

		protected override void OnRollbackOutput(DeliveryOutput output, int pass)
		{
			string guid = output.OutputID.ToString("N");

			_rollbackCommand = _rollbackCommand ?? DataManager.CreateCommand(this.Options.SqlRollbackCommand, CommandType.StoredProcedure);
			_rollbackCommand.Connection = _sqlConnection;
			_rollbackCommand.Transaction = _rollbackTransaction;

			_rollbackCommand.Parameters["@DeliveryOutputID"].Value = guid;
			_rollbackCommand.Parameters["@TableName"].Value = output.Parameters[Consts.DeliveryOutputParameters.CommitTableName];

			_rollbackCommand.ExecuteNonQuery();
			



			// This is redundant (SP already does this) but to sync our objects in memory we do it here also
			output.Status = DeliveryOutputStatus.RolledBack;

			//For new db
			/*string guid = output.OutputID.ToString("N");
			if (output.Status == DeliveryOutputStatus.Staged)
			{
				_rollbackCommand = _rollbackCommand ?? DataManager.CreateCommand(this.Options.SqlRollbackCommand, CommandType.StoredProcedure);
				_rollbackCommand.Connection = _sqlConnection;
				_rollbackCommand.Transaction = _rollbackTransaction;

				_rollbackCommand.Parameters["@DeliveryOutputID"].Value = guid;
				_rollbackCommand.Parameters["@TableName"].Value = output.Parameters[Consts.DeliveryHistoryParameters.CommitTableName];

				_rollbackCommand.ExecuteNonQuery();
				output.Status=DeliveryOutputStatus.Canceled;
			}
			else if (output.Status == DeliveryOutputStatus.Committed)
			{
				output.Status = DeliveryOutputStatus.PendingRoleBack;
			}
			else
			{
				throw new Exception("It should not happend");
			} */
			 

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
			return new SqlConnection(AppSettings.GetConnectionString(typeof(Delivery), Delivery.Consts.ConnectionStrings.SqlStagingDatabase));
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
	public abstract class MetricsImportManager<MetricsUnitT> : MetricsImportManager where MetricsUnitT : MetricsUnit
	{
		public MetricsImportManager(long serviceInstanceID, MetricsImportManagerOptions options = null)
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
