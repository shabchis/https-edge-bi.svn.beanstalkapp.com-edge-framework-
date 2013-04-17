using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.Common;
using Eggplant.Entities.Cache;

namespace Eggplant.Entities.Persistence
{
	public abstract class PersistenceConnection: IDisposable
	{
		EntityCache _cache = null;

		public PersistenceStore Store { get; private set; }

		public PersistenceConnection(PersistenceStore store)
		{
			this.Store = store;
		}

		public EntityCache Cache
		{
			get { if (_cache == null) _cache = new EntityCache(); return _cache; }
			set { _cache = value; }
		}

		public abstract void Close();

		void IDisposable.Dispose()
		{
			this.Close();	
			if (PersistenceStore.ThreadConnection == this)
				PersistenceStore.ThreadConnection = null;
		}
	}
}
