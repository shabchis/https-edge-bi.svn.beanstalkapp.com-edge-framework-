using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Eggplant.Persistence
{
	/// <summary>
	/// Monitors and switches between active connections.
	/// </summary>
	internal class PersistenceConnectionManager
	{
		List<PersistenceConnection> _connections = new List<PersistenceConnection>();
		Dictionary<string, int> _indexes = new Dictionary<string, int>();
		bool _threadLocal;

		public PersistenceConnectionManager(bool threadLocal)
		{
			_threadLocal = threadLocal;
		}

		public PersistenceConnection this[int index]
		{
			get
			{
				return _connections[index];
			}
		}

		public PersistenceConnection this[string providerName, string connectionName]
		{
			get
			{
				int index = IndexOf(providerName, connectionName);
				return index < 0 ? null : _connections[index];
			}
		}

		public PersistenceConnection Current
		{
			get
			{
				if (_connections.Count < 1)
					return null;
				else
					return _connections[0];
			}
		}

		public int IndexOf(string providerName, string connectionName)
		{
			int index;
			if (!_indexes.TryGetValue(PersistenceConnection.FormatConnectionName(providerName, connectionName), out index))
				index = -1;

			return index;
		}

		public int IndexOf(PersistenceConnection connection)
		{
			return _connections.IndexOf(connection);
		}

		public PersistenceConnection SwitchTo(PersistenceProvider provider, string connectionName)
		{
			int index = IndexOf(provider.Name, connectionName);

			// Connection is already at top of stack
			//if (index == 0)
			//	return;

			// Either reuse an existing or create a new connection
			PersistenceConnection connection = index < 0 ?
				connection = provider.NewConnection(connectionName, _threadLocal) :
				connection = _connections[index];

			if (index != 0)
				SwitchTo(connection);

			return connection;
		}

		private void SwitchTo(PersistenceConnection connection)
		{
			lock (_connections)
			{
				lock (_indexes)
				{
					if (_connections.Contains(connection))
						_connections.Remove(connection);

					_connections.Insert(0, connection);
					_indexes[connection.FullName] = 0;
				}
			}
		}

		public void Remove(PersistenceConnection connection)
		{
			lock (_connections)
			{
				lock (_indexes)
				{
					_connections.Remove(connection);
					_indexes.Remove(connection.Provider.Name);
				}
			}
		}
	}

}
