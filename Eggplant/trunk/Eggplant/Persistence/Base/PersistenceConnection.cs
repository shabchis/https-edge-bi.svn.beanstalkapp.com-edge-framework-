using System;
using System.Configuration;
using System.Data.SqlClient;
using System.Text;
using System.Threading;
using Eggplant.Model;
using Eggplant.Model.Queries;

namespace Eggplant.Persistence
{
	/// <summary>
	/// Represents a connection to a persistence store.
	/// </summary>
	public abstract class PersistenceConnection: IDisposable
	{
		#region Fields
		/*=========================*/

		bool _isOpen = false;
		bool _threadLocal = true;
		string _name;

		/// <summary>
		/// The provider for which the connection is open.
		/// </summary>
		public readonly PersistenceProvider Provider;

		/// <summary>
		/// 
		/// </summary>
		public const char ConnectionNameSeparator = '#';

		/*=========================*/
		#endregion

		#region Constructors
		/*=========================*/

		internal protected PersistenceConnection(PersistenceProvider provider)
		{
			Provider = provider;
		}

		/*=========================*/
		#endregion

		#region Properties
		/*=========================*/

		/// <summary>
		/// Indicates whether the connection is open.
		/// </summary>
		public bool IsOpen
		{
			get { return _isOpen; }
			internal set { _isOpen = value; }
		}

		/// <summary>
		/// 
		/// </summary>
		public string Name
		{
			get { return _name; }
			internal set { _name = value; }
		}

		/// <summary>
		/// The full name of the connection, including the provider name.
		/// </summary>
		public string FullName
		{
			get { return FormatConnectionName(Provider.Name, this.Name); }
		}

		/// <summary>
		/// Indicates whether the connection is open on the current thread only.
		/// </summary>
		public bool IsThreadLocal
		{
			get { return _threadLocal; }
			internal set { _threadLocal = value; }
		}

		/*=========================*/
		#endregion

		#region Methods
		/*=========================*/

		internal void Open()
		{
			OnOpen();
		}

		protected virtual void OnOpen()
		{
		}

		internal void Close()
		{
			OnClose();
		}

		protected virtual void OnClose()
		{
		}

		void IDisposable.Dispose()
		{
			if (IsOpen)
				Persistence.Close(this);
		}

		public virtual void TransactionStart()
		{
		}

		public virtual void TransactionCommit()
		{
		}

		public virtual void TransactionRollback()
		{
		}

		internal object ExecuteQuery(Query query, QueryReturn returnType)
		{
			// EXCEPTION:
			if (query.Connection != null && query.Connection != this)
				throw new Exception("The query being executed is attached to a different connection.");

			object val;
			switch (returnType)
			{
				case QueryReturn.Nothing:
					this.ExecuteQueryNoReturn(query);
					val = null;
					break;
				case QueryReturn.Value:
					val = this.ExecuteQueryAsValue(query);
					break;
				case QueryReturn.Array:
					val = this.ExecuteQueryAsArray(query);
					break;
				case QueryReturn.Reader:
					val = this.ExecuteQueryAsReader(query);
					break;
				default:
					throw new NotSupportedException("Query return type not supported.");
			}
			return val;
		}

		protected abstract void ExecuteQueryNoReturn(Query query);
		protected abstract object ExecuteQueryAsValue(Query query);
		protected abstract object[] ExecuteQueryAsArray(Query query);
		protected abstract IQueryResultReader ExecuteQueryAsReader(Query query);


		/*=========================*/
		#endregion

		#region Helpers
		/*=========================*/
		
		internal static string FormatConnectionName(string provider, string connection)
		{
			return provider + ConnectionNameSeparator + connection;
		}

		/*=========================*/
		#endregion

	}
}
