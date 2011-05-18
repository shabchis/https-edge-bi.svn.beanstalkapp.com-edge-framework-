using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SqlClient;
using System.Configuration;
using System.Threading;

namespace Eggplant.Persistence
{
	/// <summary>
	/// 
	/// </summary>
	public static class Persistence
	{
		#region Fields
		/*=========================*/

		private static Dictionary<string, PersistenceProvider> _registeredProviders = new Dictionary<string,PersistenceProvider>();
		private static PersistenceProvider _defaultProvider;

		/// <summary>
		/// Manages the global persistence connections.
		/// </summary>
		private static PersistenceConnectionManager _globalConnections = new PersistenceConnectionManager(false);
		
		/// <summary>
		/// Marked as thread static so that each thread gets a different value of this static field.
		/// TODO: DOCUMENTATION ABOUT THREAD POOLING DANGERS!!!
		/// </summary>
		[ThreadStatic]
		private static PersistenceConnectionManager _localConnections = null;
		
		/// <summary>
		/// Contains the last opened connection on the current thread. NOT IMPLEMENTED YET
		/// </summary>
		[ThreadStatic]
		private static PersistenceConnection _currentThreadGlobalConnection;

		/*=========================*/
		#endregion

		#region Properties
		/*=========================*/

		public static PersistenceConnection Current
		{
			get
			{
				PersistenceConnection connection = null;

				// Try to get an active connection from the current thread or the global list
				if (_localConnections != null)
					connection = _localConnections.Current;

				if (connection == null)
					connection = _globalConnections.Current;

				return connection;
			}
		}

		/*=========================*/
		#endregion

		#region Provider registration
		/*=========================*/

		/// <summary>
		/// 
		/// </summary>
		/// <param name="providerName"></param>
		/// <param name="provider"></param>
		public static void RegisterProvider(PersistenceProvider provider)
		{
			if (provider == null)
				throw new ArgumentNullException("provider");

			string providerName = provider.Name;
			if (String.IsNullOrWhiteSpace(providerName) || providerName.IndexOf(PersistenceConnection.ConnectionNameSeparator) >= 0)
				throw new ArgumentException("Provider name cannot be empty and cannot contain the '" + PersistenceConnection.ConnectionNameSeparator + "' character.", "provider");

			// EXCEPTION:
			if (_registeredProviders.ContainsKey(providerName))
				throw new Exception(String.Format("A provider is already registered with the name '{0}'.", providerName));

			lock (_registeredProviders)
			{
				_registeredProviders.Add(providerName, provider);
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="providerName"></param>
		public static void UnregisterProvider(string providerName)
		{
			PersistenceProvider provider;

			// EXCEPTION:
			if (!_registeredProviders.TryGetValue(providerName, out provider))
				throw new Exception(String.Format("There is no registered provider with the name '{0}'.", providerName));

			UnregisterProvider(provider);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="provider"></param>
		public static void UnregisterProvider(PersistenceProvider provider)
		{
			lock (_registeredProviders)
			{
				_registeredProviders.Remove(provider.Name);
			}
		}


		/*=========================*/
		#endregion

		#region Provider connections
		/*=========================*/

		private static PersistenceProvider GetOrThrow(string providerName)
		{
			PersistenceProvider provider;
			if (!_registeredProviders.TryGetValue(providerName, out provider))
				throw new InvalidOperationException(String.Format("There is no registered provider with the name '{0}'.", providerName));
			return provider;
		}

		
		/// <summary>
		/// 
		/// </summary>
		/// <param name="providerName"></param>
		/// <param name="threadLocal"></param>
		/// <returns>A new connection object if one was created, otherwise null if the specified connection was already open.</returns>
		public static PersistenceConnection Connect(string providerName, string connectionName = null, bool threadLocal = true)
		{
			PersistenceProvider provider = GetOrThrow(providerName);

			// Get the requested connection lists
			PersistenceConnectionManager connections;
			if (!threadLocal)
			{
				connections = _globalConnections;
			}
			else
			{
				if (_localConnections == null)
					_localConnections = new PersistenceConnectionManager(true);

				connections = _localConnections;
			}
			
			PersistenceConnection conn = connections.SwitchTo(provider, connectionName);

			// If it's not open, open it and return it, otherwise return null
			if (!conn.IsOpen)
			{
				conn.Open();
				return conn;
			}
			else
			{
				return null;
			}
		}


		/// <summary>
		/// 
		/// </summary>
		/// <param name="providerName"></param>
		public static void ForceClose(string providerName, string connectionName = null)
		{
			PersistenceProvider provider = GetOrThrow(providerName);

			PersistenceConnection conn = null;
			if (_localConnections != null)
				conn = _localConnections[providerName, connectionName];
			if (conn == null)
				conn = _globalConnections[providerName, connectionName];

			// Close and remove connection
			if (conn != null)
			{
				if (conn.IsOpen)
					Close(conn);
				
				_localConnections.Remove(conn);
			}

		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="connection"></param>
		public static void Close(PersistenceConnection connection)
		{
			if (connection == null)
				return;

			// Tell the provider to close this connection
			connection.Close();

			if (connection.IsThreadLocal)
			    _localConnections.Remove(connection);
			else
			    _globalConnections.Remove(connection);
		}
		
		/*=========================*/
		#endregion

	}
}
