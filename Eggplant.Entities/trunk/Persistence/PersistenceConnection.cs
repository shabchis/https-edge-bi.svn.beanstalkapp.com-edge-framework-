using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.Common;
using Eggplant.Entities.Cache;

namespace Eggplant.Entities.Persistence
{
	public class PersistenceConnection: IDisposable
	{
		public PersistenceStore Store { get; private set; }
		public DbConnection DbConnection {get; private set; }
		public EntityCacheManager Cache { get; set; }

		internal PersistenceConnection(PersistenceStore store, DbConnection dbConnection)
		{
			this.Store = store;
			this.DbConnection = dbConnection;
		}

		public void Close()
		{
			this.DbConnection.Close();
		}

		void IDisposable.Dispose()
		{
			DbConnection.Dispose();
			if (PersistenceStore.ThreadConnection == this)
				PersistenceStore.ThreadConnection = null;
		}
	}
}
