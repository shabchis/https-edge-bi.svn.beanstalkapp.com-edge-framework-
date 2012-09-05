using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.Common;

namespace Eggplant.Entities.Persistence
{
	public class PersistenceConnection: IDisposable
	{
		public DbConnection DbConnection {get; private set; }

		internal PersistenceConnection( DbConnection dbConnection)
		{
			this.DbConnection = dbConnection;
		}

		public void Close()
		{
			this.DbConnection.Close();
		}

		void IDisposable.Dispose()
		{
			DbConnection.Dispose();
		}
	}
}
