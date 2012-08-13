using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Win32;
using System.Data.Sql;
using System.Data.SqlClient;
using Edge.Core.Configuration;

namespace Edge.Core.Data
{
	/// <summary>
	/// Manages data source -related operations by controlling the opening and closing of
	/// connections and transactions.
	/// </summary>
	/// 
	/// <remarks>
	/// <para>
	/// All database-related operations in PT.Data are performed by means of the <b>DataManager</b> class.
	/// DataManager provides a connection and transaction managment system that ensures
	/// communication with the data source is done efficiently, by providing a precedence-based
	/// locking mechanism.
	/// </para>
	/// <para>
	/// One instance of DataManager is created for every thread. The <see cref="Current"/>>
	/// property gets the instance associated with the currently executing thread.
	/// <note>
	///	<b>DataManager</b> does not monitor the status of the executing thread. An instance is
	///	created for every thread that references <b>Current</b> at least once, but the instance
	///	will stay in memory as long as the application is running. Therefore it is necessary to call
	///	<see cref="DisposeCurrent"/> when the manager is no longer needed.
	/// </note>
	/// </para> 
	/// 
	/// <para>
	/// <b>Required configuration settings:</b>
	/// <list type="table">
	///		<listheader>
	///			<term>Full setting name</term>
	///			<description>Description</description>
	///		</listheader>
	///		<item>
	///			<term><c>Edge.Core.Data.DataManager.Connection.Timeout</c></term>
	///			<description>
	///			The <see cref="Pervasive.Data.SqlClient.SqlCommand.CommandTimeout"/> property
	///			of commands created with <see cref="CreateCommand"/> is always set to this value.
	///			</description>
	///		</item>
	///		<item>
	///			<term><c>Edge.Core.Data.DataManager.Connection.String</c></term>
	///			<description>
	///			The connection string used by all connections the DataManager creates.
	///			</description>
	///		</item>
	/// </list>
	/// </para>
	/// </remarks>
	public sealed class DataManager: IDisposable
	{
		#region Fields
		/*=========================*/
		
		/// <summary>
		/// The regular expression used to parse SQL command texts for parameter definitions.
		/// </summary>
		private static Regex _paramFinder = new Regex(@"[@$\?][A-Za-z0-9_]+:[A-Za-z0-9_]+");

		/// <summary>
		/// The prefix indicating in/out parameter direction.
		/// </summary>
		/// <value>@</value>
		internal static readonly string PrefixInOut = "$";

		/// <summary>
		/// The prefix indicating out parameter direction.
		/// </summary>
		/// <value>%</value>
		internal static readonly string PrefixOut = "?";

		/// <summary>
		/// The prefix indicating normal (in) parameter direction.
		/// </summary>
		/// <value>?</value>
		internal static readonly string PrefixIn = "@";

		/// <summary>
		/// The hash table containing all instances in use.
		/// </summary>
		private static Dictionary<Thread, DataManager> _instances = new Dictionary<Thread, DataManager>();

		/// <summary>
		/// The command timeout used to execute queries within the connection.
		/// </summary>
		private static int _commandTimeout;

		/// <summary>
		/// The connection string used to open the connection.
		/// </summary>
		private static string _connectionString;

		/// <summary>
		/// Automatically retrieve missing fields from DataItems.
		/// </summary>
		private static bool _retrieveMissing = false;

		/// <summary>
		/// Indicates whether data binding commands should run through a proxy
		/// </summary>
		private static bool _proxyMode = false;

		/// <summary>
		/// The thread this context is associated with.
		/// </summary>
		private Thread _thread = null;

		/// <summary>
		/// The currently open connection.
		/// </summary>
		private SqlConnection _connection = null;

		/// <summary>
		/// The connection key that was used to open the connection.
		/// </summary>
		private ConnectionKey _connectionKey = null;

		/// <summary>
		/// The currently active transaction.
		/// </summary>
		private SqlTransaction _transaction = null;

		/// <summary>
		/// The transaction key that was used to initiate the transaction.
		/// </summary>
		//private TransactionKey _transactionKey = null;

		/*=========================*/
		#endregion

		#region Events
		/*=========================*/

		/// <summary>
		/// Fires when the <b>DataManager</b> has been disposed.
		/// </summary>
		public event EventHandler Disposed;

		/*=========================*/
		#endregion

		#region Constructors
		/*=========================*/

		static DataManager()
		{
			/*
			string cmdtimeout = AppSettings.Get(typeof(DataManager), "Command.Timeout", false);
			if (cmdtimeout == null)
				_commandTimeout = 60;
			else
				_commandTimeout = Int32.Parse(cmdtimeout);

			_connectionString = AppSettings.Get(typeof(DataManager), "Connection.String", false);
			*/
		}

		/// <summary>
		/// Creates a new data context.
		/// </summary>
		private DataManager()
		{
			_thread = Thread.CurrentThread;
		}


		/*=========================*/
		#endregion

		#region Internal Static Methods
		/*=========================*/

		/// <summary>
		/// Creates a command object using the specified data.
		/// </summary>
		/// 
		/// <param name="text">The SQL command text.</param>
		/// <param name="type">The command type (text or stored procedure).</param>
		/// 
		/// <returns>
		/// A <see cref="Pervasive.Data.SqlClient.SqlCommand"/> object representing the command.
		/// </returns>
		/// 
		/// <remarks>
		/// <para>
		/// The command text can contain parameter definitions using the syntax
		/// summarized in <see cref="CreateParameters"/>.
		/// </para>
		/// </remarks>
		/// 
		/// <example>
		/// The following example creates a text SQL DELETE command and a stored procedure command and executes them.
		/// <code>
		/// // Create the first command (raw SQL with a parameter definition)
		/// SqlCommand deleteCommand = DataManager.CreateCommand("DELETE FROM MyTable WHERE ID = ?Integer", CommandType.Text);
		/// 
		/// // Create the second command (stored procedure with parameters)
		/// SqlCommand insertCommand = DataManager.CreateCommand("{call AddNewRecord(?Integer, ?Char, ?Char)}");
		/// 
		/// // Set parameter values
		/// deleteCommand.Parameters[0].Value = 999;
		/// insertCommand.Parameters[0].Value = 111;
		/// insertCommand.Parameters[1].Value = "First field text";
		/// insertCommand.Parameters[2].Value = "Second field text";
		/// 
		/// // Declare a key reference
		/// ConnectionKey key = null;
		/// 
		/// try
		/// {
		///		// Open the connection and retrieve the key that will be used to close the connection
		///		key = DataManager.Current.OpenConnection();
		///		
		///		// Execute the commands, dispose when done
		///		using(deleteCommand)
		///		{
		///			deleteCommand.ExecuteNonQuery();
		///		}
		///		using (insertCommand)
		///		{
		///			insertCommand.ExecuteNonQuery();
		///		}
		/// }
		/// finally
		/// {
		///		// Close the connection using the key
		///		DataManager.Current.CloseConnection(key);
		/// }
		/// </code>
		/// </example>
		public static SqlCommand CreateCommand(string text, CommandType type)
		{
			if (text == null)
				throw new ArgumentNullException("text");

			SqlCommand newCmd = new SqlCommand();
			newCmd.CommandText = text;
			newCmd.CommandType = type;
			newCmd.Connection = Current._connection;
			newCmd.Transaction = Current._transaction;
			newCmd.CommandTimeout = _commandTimeout;

			CreateParameters(newCmd);

			return newCmd;
		}

		/// <summary>
		/// Applies the currently active connection to a command object.
		/// </summary>
		/// <param name="command"></param>
		[Obsolete("Use AssociateCommands instead.")]
		public static void ApplyConnection(params SqlCommand[] commands)
		{
			Current.AssociateCommands(commands);
		}

		
		/// <summary>
		/// Creates a stored procedure command object.
		/// </summary>
		///
		/// <param name="text">The SQL command text.</param>
		///
		/// <returns>
		/// A <see cref="Pervasive.Data.SqlClient.SqlCommand"/> object representing the command.
		/// </returns>
		/// 
		/// <remarks>
		/// <para>
		/// The command text can contain parameter definitions using the syntax
		/// summarized in <see cref="CreateParameters"/>.
		/// </para>
		/// </remarks>
		/// 
		/// <example>
		/// See <see cref="CreateCommand(string,CommandType)"/>.
		/// </example>
		public static SqlCommand CreateCommand(string text)
		{
			return DataManager.CreateCommand(text, CommandType.Text);
		}

		
		/// <summary>
		/// Creates a data adapter and associates it with the data objects's commands.
		/// </summary>
		/// 
		/// <param name="dataObj">
		/// A data bound object (<see cref="DataItem"/> or <see cref="DataItemCollection"/>).
		/// </param>
		///
		/// <returns>
		/// A <see cref="Pervasive.Data.SqlClient.SqlDataAdapter"/> object associated with the
		/// <see cref="PT.Data.IDataBoundObject"/>'s commands.
		/// </returns>
		internal static SqlDataAdapter CreateAdapter(IDataBoundObject dataObj)
		{
			SqlDataAdapter adapter = new SqlDataAdapter();
			
			adapter.SelectCommand = dataObj.SelectCommand;
			adapter.InsertCommand = dataObj.InsertCommand;
			adapter.UpdateCommand = dataObj.UpdateCommand;
			adapter.DeleteCommand = dataObj.DeleteCommand;

			return adapter;
		}

        

		/// <summary>
		/// Sets the active connection of a data object.
		/// </summary>
		/// 
		/// <param name="dataObj">
		/// A data bound object (<see cref="DataItem"/> or <see cref="DataItemCollection"/>)
		/// </param>
		/// 
		/// <remarks>
		/// The method associates the data bound object's commands with the active
		/// connection and transaction of the current thread's data context.
		/// </remarks>
		private static void SetActiveConnection(IDataBoundObject dataObj)
		{
			if (dataObj.SelectCommand != null)
				dataObj.SelectCommand.Connection = DataManager.Current.Connection;

			if (dataObj.InsertCommand != null)
				dataObj.InsertCommand.Connection = DataManager.Current.Connection;

			if (dataObj.UpdateCommand != null)
				dataObj.UpdateCommand.Connection = DataManager.Current.Connection;

			if (dataObj.DeleteCommand != null)
				dataObj.DeleteCommand.Connection = DataManager.Current.Connection;

			//SetActiveTransaction(dataObj);
		}

		
		/// <summary>
		/// Sets the active transaction of a data object.
		/// </summary>
		/// 
		/// <param name="dataObj">
		/// A data bound object (<see cref="DataItem"/> or <see cref="DataItemCollection"/>).
		/// </param>
		/// 
		/// <remarks>
		/// The method associates the data bound object's commands with the active
		/// transaction of it's data context.
		/// </remarks>
		//private static void SetActiveTransaction(IDataBoundObject dataObj)
		//{
		//    if (dataObj.SelectCommand != null)
		//        dataObj.SelectCommand.Transaction = DataManager.Current.CurrentTransaction;

		//    if (dataObj.InsertCommand != null)
		//        dataObj.InsertCommand.Transaction = DataManager.Current.CurrentTransaction;

		//    if (dataObj.UpdateCommand != null)
		//        dataObj.UpdateCommand.Transaction = DataManager.Current.CurrentTransaction;

		//    if (dataObj.DeleteCommand != null)
		//        dataObj.DeleteCommand.Transaction = DataManager.Current.CurrentTransaction;
		//}


		/// <summary>
		/// Parses a command's text and creates parameters according to the direction (in/out) and type (CHAR, INTEGER, etc.)
		/// </summary>
		/// 
		/// <param name="command">The command object to expand and add parameters to.</param>
		/// 
		/// <remarks>
		/// <para>
		/// The method expands a command with the Text property set to a statement that includes parameter
		/// definitions in the following format: [prefix][name]:[type]. The method adds the parameters it recognizes to
		/// the command's <see cref="Pervasive.Data.SqlClient.SqlCommand.Parameters"/> collection.
		/// </para>
		/// <para>
		/// The prefix can be either <see cref="PrefixIn"/>, <see cref="PrefixOut"/> or <see cref="PrefixInOut"/>,
		/// representing directions input, output and input/output, respectively. The type can be one of the members of the
		/// <see cref="Pervasive.Data.SqlClient.PsqlDbType"/> enumeration.
		/// </para>
		/// <para>
		/// Example: <c>SELECT COUNT(*) + ?MyParam:Integer FROM MyTable WHERE LastName = ?MySecondParam:NVarChar</c>
		/// </para>
		/// </remarks>
		public static void CreateParameters(SqlCommand command)
		{
			MatchCollection placeHolders = _paramFinder.Matches(command.CommandText);
			string commandText = command.CommandText;
			int offsetChange = 0;

			for(int i = 0; i < placeHolders.Count; i++)
			{
				string name = placeHolders[i].Value.Substring(1).Split(':')[0];
				string type = placeHolders[i].Value.Substring(1).Split(':')[1];
				string indicator = placeHolders[i].Value[0].ToString();

				// Replace placeholder with actual parameter
				commandText = commandText.Remove(placeHolders[i].Index + offsetChange, placeHolders[i].Length);
				commandText = commandText.Insert(placeHolders[i].Index + offsetChange, PrefixIn + name);
				offsetChange += (PrefixIn + name).Length - placeHolders[i].Length;
				
				// Ignore the parameter if it already has been added
				if (command.Parameters.Contains(PrefixIn + name))
					continue;
				
				SqlDbType dbType = (SqlDbType) Enum.Parse(typeof(SqlDbType), type, true);

				SqlParameter param = new SqlParameter();
				param.SqlDbType = dbType;
				param.ParameterName = PrefixIn + name;

				// Set parameter directions
				if (indicator == PrefixInOut)
				{
					param.Direction = ParameterDirection.InputOutput;
				}
				else if (indicator == PrefixOut)
				{
					param.Direction = ParameterDirection.Output;
				}

				// Add the parameter
				command.Parameters.Add(param);
			}

			// For stored procs, leave only the name
			if (command.CommandType == CommandType.StoredProcedure)
				commandText = commandText.Split('(')[0];

			// Replace command text to proper version
			command.CommandText = commandText;
		}

		
		/*=========================*/
		#endregion

		#region Internal Methods
		/*=========================*/

		/// <summary>
		/// Initiates a new transaction.
		/// </summary>
		/// 
		/// <returns>
		/// A <see cref="TransactionKey"/> if a new transaction is initiated, otherwise null.
		/// </returns>
		//public TransactionKey BeginTransaction()
		//{
		//    return BeginTransaction(null);
		//}

		
		/// <summary>
		/// Initiates a new transaction and associates it with the specified data bound object.
		/// </summary>
		/// 
		/// <param name="dataObj">A data bound object that will use the transaction.</param>
		/// 
		/// <returns>
		/// A <see cref="TransactionKey"/> if a new transaction is initiated, otherwise null.
		/// </returns>
		//public TransactionKey BeginTransaction(IDataBoundObject dataObj)
		//{

		//    // This is the key used to close the connection.
		//    TransactionKey key = null;

		//    // If a transaction isn't in progress, begin a new one
		//    if (_connection != null && _transaction != null)
		//    {
		//        _transaction = _connection.BeginTransaction();
		//        key = new TransactionKey();
		//    }
			
		//    // Associate the data bound object's commands with the current transaction
		//    if (dataObj != null)
		//        SetActiveTransaction(dataObj);

		//    if (key != null)
		//    {
		//        _transactionKey = key;
		//    }

		//    return key;
		//}


		/// <summary>
		/// Commits the current transaction if the specified key is the current valid key.
		/// </summary>
		/// <param name="key">
		/// The key returned by <see cref="BeginTransaction"/>. If key is null or does not match the
		/// current valid key, the method does not do anything.
		/// </param>
		//public void CommitTransaction(TransactionKey  key)
		//{
		//    if (
		//        key != null &&
		//        _transactionKey == key &&
		//        _transaction != null
		//        )
		//    {
		//        _transaction.Commit();
		//        _transaction.Dispose();
		//        _transaction = null;
		//        _transactionKey = null;
		//    }
		//}

		
		/// <summary>
		/// Rolls back the current transaction if the specified key is the current valid key.
		/// </summary>
		/// <param name="key">
		/// The key returned by <see cref="BeginTransaction"/>. If key is null or does not match the
		/// current valid key, the method does not do anything.
		/// </param>
		//public void RollbackTransaction(TransactionKey  key)
		//{
		//    if (
		//        key != null &&
		//        _transactionKey == key &&
		//        _transaction != null
		//        )
		//    {
		//        _transaction.Rollback();
		//        _transaction.Dispose();
		//        _transaction = null;
		//        _transactionKey = null;
		//    }
		//}


		
		/// <summary>
		/// Removes a disposed DataManager instance from the instances hashtable.
		/// </summary>
		/// <param name="sender">The DataManager that has been disposed.</param>
		/// <param name="e">Empty event args.</param>
		private static void InstanceDisposed(object sender, EventArgs e)
		{
			DataManager context = sender as DataManager;
			if (context != null)
				_instances.Remove(context._thread);
		}


		/*=========================*/
		#endregion
		
		#region Internal Properties
		/*=========================*/

		/// <summary>
		/// Gets the active connection.
		/// </summary>
		internal SqlConnection Connection
		{
			get
			{
				return _connection;
			}
		}

		
		/// <summary>
		/// Gets the current connection key.
		/// </summary>
		internal object ConnectionKey
		{
			get
			{
				return _connectionKey;
			}
		}

		
		/// <summary>
		/// Gets the active transaction.
		/// </summary>
		//internal SqlTransaction CurrentTransaction
		//{
		//    get
		//    {
		//        return _transaction;
		//    }
		//}


		/*=========================*/
		#endregion

		#region Public Properties
		/*=========================*/

		/*=========================*/
		#endregion

		#region Public Methods
		/*=========================*/

		/// <summary>
		/// Opens a new connection if none is currently open.
		/// </summary>
		/// <returns>
		///	When a new connection is opened, a key object that enables closing the connection.
		///	Null if the connection is already open.
		///	</returns>
		public ConnectionKey OpenConnection()
		{
			return OpenConnection(null, null);
		}

		public ConnectionKey OpenConnection(SqlConnection connection)
		{
			return OpenConnection(null, connection);
		}

		public ConnectionKey OpenConnection(IDataBoundObject dataObj)
		{
			return OpenConnection(dataObj, null);
		}

		/// <summary>
		/// Manages connection open based on sequence.
		/// </summary>
		/// 
		/// <param name="dataObj">If not null, associates the connection with the object's data commands.</param>
		/// 
		/// <returns>
		///	When opening a new connection, an object that can be used to close the connection.
		///	Null if the connection is already open.
		///	</returns>
		public ConnectionKey OpenConnection(IDataBoundObject dataObj, SqlConnection externalConnection)
		{
			//Check for a valid connection string
			if (externalConnection == null && (_connectionString == "" || _connectionString == null))
				return null;

			// This is the key used to close the connection.
			ConnectionKey key = null;

			// If a connection doesn't exist, open a new one
			// (second condition is just in case, should never happen because we nullify the connection when closing it)
			if (_connection == null || _connection.State == ConnectionState.Closed)
			{
				if (externalConnection != null)
				{
					_connection = externalConnection;
				}
				else
				{
					_connection = new SqlConnection(_connectionString);
					key = new ConnectionKey();
				}
			}

			// Associate the data bound object's commands with the current connection
			if (dataObj != null)
			{
				SetActiveConnection(dataObj);
			}

			if (key != null)
			{
				_connectionKey = key;
				_connection.Open();
			}

			return key;
		}
		/// <summary>
		/// Closes the open connection if <pararef name="key">key</pararef> matches the active key.
		/// </summary>
		/// 
		/// <param name="key">
		/// The key returned by <see cref="OpenConnection"/>. If key is null or does not match the
		/// current valid key, the method does not do anything.
		/// </param>
		/// 
		public void CloseConnection(ConnectionKey key)
		{
			if (
					key != null &&
					_connectionKey == key &&
					_connection != null
				)
			{
				if (_connection.State == ConnectionState.Open)
				{
					if (_transaction != null)
						CommitTransaction();

					_connection.Close();
				}

				_transaction = null;
				_connection = null;
				_connectionKey = null;
			}
		}


		/// <summary>
		/// Ignores any keys and forces the connection to close.
		/// </summary>
		public void ForceCloseConnection()
		{
			_connectionKey = null;

			if (_connection != null && _connection.State == ConnectionState.Open)
			{
				if (_transaction != null)
					CommitTransaction();
				
				_connection.Close();
			}

			_transaction = null;
			_connection = null;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <typeparam name="DataItemT"></typeparam>
		/// <param name="command"></param>
		/// <returns></returns>
		public DataItemT[] GetItems<DataItemT>(SqlCommand command) where DataItemT: DataItem
		{
			return null;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <typeparam name="DataItemT"></typeparam>
		/// <param name="query"></param>
		/// <returns></returns>
		public DataItemT[] GetItems<DataItemT>(string query) where DataItemT: DataItem
		{
			return GetItems<DataItemT>((SqlCommand)null);
		}

		/// <summary>
		/// Associates the specified commands with the currently active connection and transaction.
		/// </summary>
		/// <param name="command"></param>
		public void AssociateCommands(params SqlCommand[] commands)
		{
			foreach (SqlCommand command in commands)
			{
				command.Connection = _connection;
				if (_transaction != null)
					command.Transaction = _transaction;
			}
		}

		public void StartTransaction(IsolationLevel iso)
		{
			if (_transaction != null)
				throw new InvalidOperationException("A transaction is already active.");

			if (_connection == null)
				throw new InvalidOperationException("OpenConnection() must be called before a transaction can be started.");

			_transaction = _connection.BeginTransaction(iso);
		}

		public void StartTransaction(SqlTransaction externalTransaction)
		{
			if (_transaction != null)
				throw new InvalidOperationException("A transaction is already active.");

			if (_connection == null)
				throw new InvalidOperationException("OpenConnection() must be called before a transaction can be started.");

			_transaction = externalTransaction;
		}

		public void StartTransaction()
		{
			StartTransaction(IsolationLevel.Unspecified);
		}

		public void CommitTransaction()
		{
			if (_transaction == null)
				throw new InvalidOperationException("There is no active transaction to commit.");

			_transaction.Commit();
			_transaction = null;
		}

		public void RollbackTransaction()
		{
			if (_transaction == null)
				throw new InvalidOperationException("There is no active transaction to roll back.");

			_transaction.Rollback();
			_transaction = null;
		}

		/// <summary>
		/// Clears the transaction without comitting or rolling it back.
		/// </summary>
		public void ClearTransaction()
		{
			_transaction = null;
		}

		/*=========================*/
		#endregion

		#region Public Static Methods
		/*=========================*/
		
		/// <summary>
		/// Formats a guid in the format expected by the database.
		/// </summary>
		/// 
		/// <param name="guid">A non-empty global unique ID.</param>
		/// 
		/// <returns>
		/// A 38-character -long string enclosed in
		/// curly braces. The hexadecimal digits A-F are capitalized.
		/// </returns>
		/// 
		/// <example>
		/// <c>DataManager.FormatGuid(new Guid(0xa,0xb,0xc,0,1,2,3,4,5,6,7))</c> returns the string "{0000000A-000B-000C-0001-020304050607}".
		/// </example>
		public static string FormatGUID(Guid guid)
		{
			if (guid == Guid.Empty)
				throw new ArgumentException("Only non-empty GUIDs can be formatted.", "guid");

			return "{" + guid.ToString().ToUpper() + "}";
		}

	
		/// <summary>
		/// Disposes of the current context if it still exists.
		/// </summary>
		public static void DisposeCurrent()
		{
			if (_instances.ContainsKey(Thread.CurrentThread))
			{
				DataManager current = _instances[Thread.CurrentThread] as DataManager;
				current.Dispose();
			}
		}


		/*=========================*/
		#endregion

		#region Public Static Properties
		/*=========================*/

		/// <summary>
		/// Gets the data context associated with the current thread.
		/// </summary>
		public static DataManager Current
		{
			get
			{
				DataManager current = null;
				if (!_instances.ContainsKey(Thread.CurrentThread))
				{
					current = new DataManager();
					current.Disposed += new EventHandler(InstanceDisposed);

					lock (_instances)
					{
						_instances.Add(Thread.CurrentThread, current);
					}
				}
				else
					current = _instances[Thread.CurrentThread] as DataManager;

				return current;
			}
		}

		public static int CommandTimeout
		{
			set
			{
				_commandTimeout = value;
			}
			get
			{
				return _commandTimeout;
			}
		}

		public static string ConnectionString
		{
			set
			{
				_connectionString = value;
			}
			get
			{
				return _connectionString;
			}
		}

		public static bool AutoRetrieveMissingFields
		{
			set
			{
				_retrieveMissing = value;
			}
			get
			{
				return _retrieveMissing;
			}
		}

		public static bool ProxyMode
		{
			get
			{
				return _proxyMode;
			}
			set
			{
				_proxyMode = value;
			}
		}

		/*=========================*/
		#endregion

		#region IDisposable Members
		/*=========================*/

		/// <summary>
		/// Closes and disposes of the active transaction and connection.
		/// </summary>
		public void Dispose()
		{
			//if (_transaction != null)
			//    _transaction.Dispose();

			if (_connection != null)
				_connection.Dispose();

			// Fire the disposed event
			if (this.Disposed != null)
				this.Disposed(this, EventArgs.Empty);
		}


		/*=========================*/
		#endregion

		public static SqlBulkCopy CreateBulkCopy(SqlBulkCopyOptions options)
		{
			return new SqlBulkCopy(DataManager.Current.Connection, options, null);
		}

		public static object Normalize(object value, object nullValue = null)
		{
			object returnObject;
			if (value == null)
				returnObject = nullValue ?? DBNull.Value;
			else
			{
				if (value is Enum)
					returnObject = (int)value;
				else
					returnObject = value;
			}
			return returnObject;
		}

	}

	/// <summary>
	/// Used to open or close connections.
	/// </summary>
	public class ConnectionKey: IDisposable
	{
		/// <summary>
		/// Creates a new connection key.
		/// </summary>
		internal ConnectionKey()
		{
		}

		#region IDisposable Members

		/// <summary>
		/// Closes the connection if it was opened using this key.
		/// </summary>
		public void Dispose()
		{
			DataManager.Current.CloseConnection(this);
		}

		#endregion
	}

	
	/// <summary>
	/// Used to access transactions.
	/// </summary>
	//public class TransactionKey
	//{
	//    /// <summary>
	//    /// Creates a new transaction key.
	//    /// </summary>
	//    internal TransactionKey()
	//    {
	//    }
	//}

	public static class SqlDataReaderExtensions
	{
		public static T Get<T>(this SqlDataReader reader, string field, T nullVal = default(T))
		{
			var val = reader[field];
			if (val is DBNull)
				return nullVal;
			else
				return (T)val;
		}

		public static ConvertT Convert<SourceT, ConvertT>(this SqlDataReader reader, string field, Func<SourceT, ConvertT> convertFunc, SourceT nullVal = default(SourceT))
		{
			SourceT val = reader.Get<SourceT>(field, nullVal);
			return convertFunc(val);
		}
	}

}
