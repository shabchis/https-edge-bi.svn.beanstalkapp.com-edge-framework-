using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.IO;
using System.Data.Common;
using System.Data;

namespace Eggplant.Entities.Persistence
{
	public abstract class PersistenceStore
	{
		public string ConnectionString { get; set; }
		public TimeSpan ConnectionTimeout { get; set; }
		public TimeSpan DefaultCommandTimeout { get; set; }

		public abstract PersistenceConnection Connect();

		[ThreadStatic]
		internal static PersistenceConnection ThreadConnection;

		public PersistenceConnection ConnectThread()
		{
			if (ThreadConnection != null)
				throw new InvalidOperationException("Cannot connect the current thread to this store because a different connection is still active on this thread.");

			PersistenceConnection connection = this.Connect();
			ThreadConnection = connection;

			return connection;
		}

		public abstract DbParameter NewDbParameter(string name);
		public abstract DbCommand NewDbCommand(string commandText = null, CommandType commandType = CommandType.Text);
		public abstract PersistenceAdapter NewAdapter(DbDataReader reader);

		/*
		public static PersistenceStore LoadFromConfiguration(string configurationName)
		{
			throw new NotImplementedException();
		}

		public static PersistenceStore LoadXml(XmlReader xml, bool closeReader = false)
		{
			throw new NotImplementedException();
		}

		public static PersistenceStore LoadXml(string xml)
		{
			return LoadXml(new XmlTextReader(new StringReader(xml)), true);
		}
		*/
	}
}
