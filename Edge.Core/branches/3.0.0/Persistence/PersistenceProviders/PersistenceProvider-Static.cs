using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SqlClient;
using System.Configuration;
using System.Threading;

namespace Eggplant.Persistence
{
	public class PersistenceConnection: IDisposable
	{
		public readonly PersistenceProvider Provider;
		bool _isOpen = false;

		public bool IsOpen
		{
			get { return _isOpen; }
			internal set { _isOpen = value; }
		}

		internal PersistenceConnection(PersistenceProvider provider)
		{
			Provider = provider;
		}

		void IDisposable.Dispose()
		{
			if (IsOpen)
				PersistenceProvider.Close(this);
		}
	}

	public partial class PersistenceProvider
	{
		private static Dictionary<Thread, List<PersistenceConnection>> _activeConnections;
		private static Dictionary<string, PersistenceProvider> _registeredProviders;

		public static void Register(string providerName, PersistenceProvider provider)
		{
			// EXCEPTION:
			if (_registeredProviders.ContainsKey(providerName))
				throw new Exception("A provider is already registered under this name.");

			_registeredProviders.Add(providerName, provider);
		}

		public static void Unregister(string providerName)
		{
			// EXCEPTION:
			if (!_registeredProviders.ContainsKey(providerName))
				throw new Exception("There is no provider registered under this name.");

			_registeredProviders.Remove(providerName);
		}

		public static PersistenceConnection Open(string providerName)
		{
			// EXCEPTION:
			PersistenceProvider provider;
			if (!_registeredProviders.TryGetValue(providerName, out provider))
				throw new InvalidOperationException(String.Format("There is no provider registered under the name \"{0}\".", providerName));

			List<PersistenceConnection> connections;
			int currentIndex = -1;

			// Try to locate the index of the connection
			if (_activeConnections.TryGetValue(Thread.CurrentThread, out connections) && connections != null && connections.Count > 0)
			{
				for (int i = 0; i < connections.Count; i++)
				{
					if (connections[i].Provider == provider)
					{
						currentIndex = i;
						break;
					}
				}
			}

			PersistenceConnection conn;
			if (currentIndex >= 0)
			{
				// Remove from this position, we're going to add it to the end of the list
				conn = connections[currentIndex];
				connections.RemoveAt(currentIndex);
			}
			else
			{
				if (connections == null)
				{
					connections = new List<PersistenceConnection>();
					_activeConnections.Add(Thread.CurrentThread, connections);
				}

				conn = new PersistenceConnection(provider);
			}

			// Add the connection to the end of the list
			connections.Add(conn);

			// If it's not open, open it and return it, otherwise return null
			if (!provider.IsOpen)
			{
				provider.Open();
				return conn;
			}
			else
			{
				return null;
			}
		}

		public static void ForceClose(string providerName)
		{
			PersistenceProvider provider;
			if (!_registeredProviders.TryGetValue(providerName, out provider))
				return;
	
			List<PersistenceConnection> connections;
			int currentIndex = -1;

			// Try to locate the index of the connection
			if (_activeConnections.TryGetValue(Thread.CurrentThread, out connections) && connections != null && connections.Count > 0)
			{
				for (int i = 0; i < connections.Count; i++)
				{
					if (connections[i].Provider == provider)
					{
						currentIndex = i;
						break;
					}
				}
			}

			if (currentIndex < -1)
				return;

			PersistenceConnection conn = connections[currentIndex];
			if (conn.IsOpen)
				Close(conn);

			connections.RemoveAt(currentIndex);
		}

		public static void Close(PersistenceConnection connection)
		{
			throw new NotImplementedException();
		}

		public static PersistenceProvider Current
		{
			get
			{
				List<PersistenceConnection> connections;

				// Try to get the value, return null if unavailable
				if (!_activeConnections.TryGetValue(Thread.CurrentThread, out connections) || connections == null || connections.Count < 1)
					return null;

				// Return the last connection entered
				return connections[connections.Count - 1].Provider;
			}
		}

	}

}
