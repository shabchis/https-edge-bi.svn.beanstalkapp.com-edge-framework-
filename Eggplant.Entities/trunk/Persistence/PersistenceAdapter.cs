using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eggplant.Entities.Cache;

namespace Eggplant.Entities.Persistence
{
	public abstract class PersistenceAdapter: IDisposable
	{
		public PersistenceConnection Connection { get; private set; }

		public PersistenceAdapter(PersistenceConnection connection)
		{
			this.Connection = connection;
		}

		public abstract bool HasField(string field);
		public abstract object GetField(string field);
		public abstract void SetField(string field, object value);
		public abstract void Dispose();
		public abstract bool NextResultSet();

		public abstract bool Read();
	}
}
