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
	public abstract class MetricsImportManager:DeliveryImportManager
	{
		#region Fields
		/*=========================*/

		private SqlConnection _sqlConnection;

		public Dictionary<string, Measure> Measures { get; private set; }
		public Dictionary<string, Segment> SegmentTypes { get; private set; }
		public MetricsImportManagerOptions Options { get; private set; }

		/*=========================*/
		#endregion

		#region Constructors
		/*=========================*/

		public MetricsImportManager(long serviceInstanceID, MetricsImportManagerOptions options = null) : base(serviceInstanceID)
		{
			options = options ?? new MetricsImportManagerOptions();
			options.StagingConnectionString = options.StagingConnectionString ?? AppSettings.GetConnectionString(this, Consts.ConnectionStrings.StagingDatabase);
			options.SqlPrepareCommand = options.SqlPrepareCommand ?? AppSettings.Get(this, Consts.AppSettings.SqlPrepareCommand, throwException: false);
			options.SqlCommitCommand = options.SqlCommitCommand ?? AppSettings.Get(this, Consts.AppSettings.SqlCommitCommand, throwException: false);
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
			this.HistoryEntryParameters.Add(Consts.DeliveryHistoryParameters.TablePerfix, this._tablePrefix);

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

			this.HistoryEntryParameters.Add(Consts.DeliveryHistoryParameters.MeasureOltpFieldsSql, measuresFieldNamesSQL.ToString());
			this.HistoryEntryParameters.Add(Consts.DeliveryHistoryParameters.MeasureNamesSql, measuresNamesSQL.ToString());
			if (string.IsNullOrEmpty(measuresValidationSQL.ToString()))
				Log.Write("No measures marked for checksum validation; there will be no validation before the final commit.", LogMessageType.Warning);
			else
				this.HistoryEntryParameters.Add(Consts.DeliveryHistoryParameters.MeasureValidateSql, measuresValidationSQL.ToString());

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
		
		SqlCommand _prepareCommand = null;
		SqlCommand _validateCommand = null;
		const int Prepare_PREPARE_PASS = 0;
		const int Prepare_VALIDATE_PASS = 1;
		const string ValidationTable = "Commit_FinalMetrics";

		protected override int PreparePassCount
		{
			get { return 2; }
		}

		protected override void OnBeginPrepare()
		{
			if (String.IsNullOrWhiteSpace(this.Options.SqlPrepareCommand))
				throw new ConfigurationException("Options.SqlPrepareCommand is empty.");

			_sqlConnection = NewDeliveryDbConnection();
			_sqlConnection.Open();
		}

		protected override void OnPrepare(int pass)
		{
			DeliveryHistoryEntry processedEntry = this.CurrentDelivery.History.Last(entry => entry.Operation == DeliveryOperation.Imported);
			if (processedEntry == null)
				throw new Exception("This delivery has not been imported yet (could not find an 'Imported' history entry).");

			// get this from last 'Processed' history entry
			string measuresFieldNamesSQL = processedEntry.Parameters[Consts.DeliveryHistoryParameters.MeasureOltpFieldsSql].ToString();
			string measuresNamesSQL = processedEntry.Parameters[Consts.DeliveryHistoryParameters.MeasureNamesSql].ToString();

			string tablePerfix = processedEntry.Parameters[Consts.DeliveryHistoryParameters.TablePerfix].ToString();
			string deliveryId = this.CurrentDelivery.DeliveryID.ToString("N");

			if (pass == Prepare_PREPARE_PASS)
			{
				// ...........................
				// PREPARE data

				_prepareCommand = _prepareCommand ?? DataManager.CreateCommand(this.Options.SqlPrepareCommand, CommandType.StoredProcedure);
				_prepareCommand.Connection = _sqlConnection;

				_prepareCommand.Parameters["@DeliveryID"].Size = 4000;
				_prepareCommand.Parameters["@DeliveryID"].Value = deliveryId;
				_prepareCommand.Parameters["@DeliveryTablePrefix"].Size = 4000;
				_prepareCommand.Parameters["@DeliveryTablePrefix"].Value = tablePerfix;
				_prepareCommand.Parameters["@MeasuresNamesSQL"].Size = 4000;
				_prepareCommand.Parameters["@MeasuresNamesSQL"].Value = measuresNamesSQL;
				_prepareCommand.Parameters["@MeasuresFieldNamesSQL"].Size = 4000;
				_prepareCommand.Parameters["@MeasuresFieldNamesSQL"].Value = measuresFieldNamesSQL;
				_prepareCommand.Parameters["@CommitTableName"].Size = 4000;
				_prepareCommand.Parameters["@CommitTableName"].Direction = ParameterDirection.Output;

				try { _prepareCommand.ExecuteNonQuery(); }
				catch (Exception ex)
				{
					throw new Exception(String.Format("Delivery {0} failed during Prepare.", deliveryId), ex);
				}

				this.HistoryEntryParameters[Consts.DeliveryHistoryParameters.CommitTableName] = _prepareCommand.Parameters["@CommitTableName"].Value;
			}
			else if (pass == Prepare_VALIDATE_PASS)
			{
				object totalso;

				if (processedEntry.Parameters.TryGetValue(Consts.DeliveryHistoryParameters.ChecksumTotals, out totalso))
				{
					var totals = (Dictionary<string, double>)totalso;

					object sql;
					if (processedEntry.Parameters.TryGetValue(Consts.DeliveryHistoryParameters.MeasureValidateSql, out sql))
					{

						string measuresValidateSQL = (string)sql;
						measuresValidateSQL = measuresValidateSQL.Insert(0, "SELECT ");
						measuresValidateSQL = measuresValidateSQL + string.Format("\nFROM {0}_{1} \nWHERE DeliveryID=@DeliveryID:Nvarchar", tablePerfix, ValidationTable);

						SqlCommand validateCommand = DataManager.CreateCommand(measuresValidateSQL);
						validateCommand.Connection = _sqlConnection;
						validateCommand.Parameters["@DeliveryID"].Value = this.CurrentDelivery.DeliveryID.ToString("N");
						using (SqlDataReader reader = validateCommand.ExecuteReader())
						{
							if (reader.Read())
							{
								var results = new StringBuilder();
								foreach (KeyValuePair<string, double> total in totals)
								{
									if (reader[total.Key] is DBNull)
									{

										if (total.Value == 0)
											Log.Write(string.Format("[zero totals] {0} has no data or total is 0 in table {1} for target period {2}", total.Key, ValidationTable, CurrentDelivery.TargetPeriod), LogMessageType.Information);
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
											Log.Write(string.Format("[zero totals] {0} has no data or total is 0 in table {1} for target period {2}", total.Key, ValidationTable, CurrentDelivery.TargetPeriod), LogMessageType.Information);


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

		/*=========================*/
		#endregion

		#region Commit
		/*=========================*/

		SqlTransaction _commitTransaction = null;
		SqlCommand _commitCommand = null;

		protected override int CommitPassCount
		{
			get { return 1; }
		}

		protected override void OnBeginCommit()
		{
			if (String.IsNullOrWhiteSpace(this.Options.SqlCommitCommand))
				throw new ConfigurationException("Options.SqlCommitCommand is empty.");

			_sqlConnection = NewDeliveryDbConnection();
			_sqlConnection.Open();

			_commitTransaction = _sqlConnection.BeginTransaction("Delivery Commit");
		}

		protected override void OnCommit(int pass)
		{
			 DeliveryHistoryEntry processedEntry = this.CurrentDelivery.History.Last(entry => entry.Operation == DeliveryOperation.Imported);
			if (processedEntry == null)
				throw new Exception("This delivery has not been imported yet (could not find an 'Imported' history entry).");

			DeliveryHistoryEntry preparedEntry = this.CurrentDelivery.History.Last(entry => entry.Operation == DeliveryOperation.Prepared);
			if (preparedEntry == null)
				throw new Exception("This delivery has not been prepared yet (could not find an 'Prepared' history entry).");

			// get this from last 'Processed' history entry
			string measuresFieldNamesSQL = processedEntry.Parameters[Consts.DeliveryHistoryParameters.MeasureOltpFieldsSql].ToString();
			string measuresNamesSQL = processedEntry.Parameters[Consts.DeliveryHistoryParameters.MeasureNamesSql].ToString();

			string tablePerfix = processedEntry.Parameters[Consts.DeliveryHistoryParameters.TablePerfix].ToString();
			string deliveryId = this.CurrentDelivery.DeliveryID.ToString("N");


			// ...........................
			// COMMIT data to OLTP

			_commitCommand = _commitCommand ?? DataManager.CreateCommand(this.Options.SqlCommitCommand, CommandType.StoredProcedure);
			_commitCommand.Connection = _sqlConnection;
			_commitCommand.Transaction = _commitTransaction;

			_commitCommand.Parameters["@DeliveryFileName"].Size = 4000;
			_commitCommand.Parameters["@DeliveryFileName"].Value = tablePerfix;
			_commitCommand.Parameters["@CommitTableName"].Size = 4000;
			_commitCommand.Parameters["@CommitTableName"].Value = preparedEntry.Parameters["CommitTableName"];
			_commitCommand.Parameters["@MeasuresNamesSQL"].Size = 4000;
			_commitCommand.Parameters["@MeasuresNamesSQL"].Value = measuresNamesSQL;
			_commitCommand.Parameters["@MeasuresFieldNamesSQL"].Size = 4000;
			_commitCommand.Parameters["@MeasuresFieldNamesSQL"].Value = measuresFieldNamesSQL;
			_commitCommand.Parameters["@Signature"].Size = 4000;
			_commitCommand.Parameters["@Signature"].Value = this.CurrentDelivery.Signature; ;
			_commitCommand.Parameters["@DeliveryIDsPerSignature"].Size = 4000;
			_commitCommand.Parameters["@DeliveryIDsPerSignature"].Direction = ParameterDirection.Output;
			_commitCommand.Parameters["@DeliveryID"].Size = 4000;
			_commitCommand.Parameters["@DeliveryID"].Value = deliveryId;


			try
			{
				_commitCommand.ExecuteNonQuery();
				//	_commitTransaction.Commit();

				string deliveryIDsPerSignature = _commitCommand.Parameters["@DeliveryIDsPerSignature"].Value.ToString();

				string[] existDeliveries;
				if ((!string.IsNullOrEmpty(deliveryIDsPerSignature) && deliveryIDsPerSignature != "0"))
				{
					_commitTransaction.Rollback();
					existDeliveries = deliveryIDsPerSignature.Split(',');
					List<Delivery> deliveries = new List<Delivery>();
					foreach (string existDelivery in existDeliveries)
					{
						deliveries.Add(Delivery.Get(Guid.Parse(existDelivery)));
					}
					throw new DeliveryConflictException(string.Format("Deliveries with the same signature are already committed in the database\n Deliveries:\n {0}:", deliveryIDsPerSignature)) { ConflictingDeliveries = deliveries.ToArray() };
				}
				else
					//already updated by sp, this is so we don't override it
					this.CurrentDelivery.IsCommited = true;
			}
			finally
			{
				this.State = DeliveryImportManagerState.Idle;
			}
		}

		protected override void OnEndCommit(Exception ex)
		{
			if (_commitTransaction != null)
			{
				if (ex == null)
					_commitTransaction.Commit();
				else
					_commitTransaction.Rollback();
			}
			this.State = DeliveryImportManagerState.Idle;
		}

		protected override void OnDisposeCommit()
		{
			if (_commitTransaction != null)
				_commitTransaction.Dispose();
		}

		/*=========================*/
		#endregion

		#region Rollback
		/*=========================*/

		SqlCommand _rollbackCommand = null;
		SqlTransaction _rollbackTransaction = null;

		protected override void OnBeginRollback()
		{
			if (String.IsNullOrWhiteSpace(this.Options.SqlRollbackCommand))
				throw new ConfigurationException("Options.SqlRollbackCommand is empty.");

			_sqlConnection = NewDeliveryDbConnection();
			_sqlConnection.Open();
			_rollbackTransaction = _sqlConnection.BeginTransaction("Delivery Rollback");
		}

		protected override void OnRollback(int pass)
		{
			DeliveryHistoryEntry prepareEntry = null;
			string guid = this.CurrentDelivery.DeliveryID.ToString("N");
			IEnumerable<DeliveryHistoryEntry> prepareEntries = this.CurrentDelivery.History.Where(entry => entry.Operation == DeliveryOperation.Prepared);
			if (prepareEntries != null && prepareEntries.Count() > 0)
				prepareEntry = (DeliveryHistoryEntry)prepareEntries.Last();
			if (prepareEntry == null)
				throw new Exception(String.Format("The delivery '{0}' has never been committed so it cannot be rolled back.", guid));

			_rollbackCommand = _rollbackCommand ?? DataManager.CreateCommand(this.Options.SqlRollbackCommand, CommandType.StoredProcedure);
			_rollbackCommand.Connection = _sqlConnection;
			_rollbackCommand.Transaction = _rollbackTransaction;

			_rollbackCommand.Parameters["@DeliveryID"].Value = guid;
			_rollbackCommand.Parameters["@TableName"].Value = prepareEntry.Parameters[Consts.DeliveryHistoryParameters.CommitTableName];

			_rollbackCommand.ExecuteNonQuery();
			this.CurrentDelivery.IsCommited = false;
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
		public MetricsImportManager(long serviceInstanceID, MetricsImportManagerOptions options = null) : base(serviceInstanceID, options)
		{
		}

		public override void ImportMetrics(MetricsUnit metrics)
		{
			this.ImportMetrics((MetricsUnitT)metrics);
		}

		public abstract void ImportMetrics(MetricsUnitT metrics);
	}
}
