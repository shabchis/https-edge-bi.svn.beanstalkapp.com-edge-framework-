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
		EntityCacheManager _cache = null;

		public PersistenceStore Store { get; private set; }
		public DbConnection DbConnection {get; private set; }

		internal PersistenceConnection(PersistenceStore store, DbConnection dbConnection)
		{
			this.Store = store;
			this.DbConnection = dbConnection;
		}

		public EntityCacheManager Cache
		{
			get { if (_cache == null) _cache = new EntityCacheManager(); return _cache; }
			set { _cache = value; }
		}

		public void Close()
		{
			this.DbConnection.Close();
		}

		public PersistenceAdapter CreateAdapter(DbDataReader reader)
		{
			return this.Store.CreateAdapter(this, reader);
		}

		void IDisposable.Dispose()
		{
			DbConnection.Dispose();
			if (PersistenceStore.ThreadConnection == this)
				PersistenceStore.ThreadConnection = null;
		}
	}
}
