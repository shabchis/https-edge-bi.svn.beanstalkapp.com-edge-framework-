using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SqlClient;

namespace Eggplant.Entities.Persistence.SqlServer
{
	public class SqlPersistenceConnection: PersistenceConnection
	{
		public SqlConnection DbConnection { get; private set; }

		public SqlPersistenceConnection(SqlPersistenceStore store, SqlConnection innerConnection): base(store)
		{
			this.DbConnection = innerConnection;
		}

		public override void Close()
		{
			if (this.DbConnection.State != System.Data.ConnectionState.Closed)
				this.DbConnection.Close();
		}
	}
}
