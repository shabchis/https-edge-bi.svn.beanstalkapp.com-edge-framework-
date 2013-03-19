using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SqlClient;
using System.Data;

namespace Eggplant.Entities.Persistence.SqlServer
{
	public class SqlPersistenceStore : PersistenceStore
	{
		public override PersistenceConnection Connect()
		{
			var innerConnection = new System.Data.SqlClient.SqlConnection(this.ConnectionString);
			innerConnection.Open();
			return new SqlPersistenceConnection(this, innerConnection);
		}

		public override PersistenceAction NewPersistenceAction()
		{
			return new SqlPersistenceAction();
		}
	}
}
