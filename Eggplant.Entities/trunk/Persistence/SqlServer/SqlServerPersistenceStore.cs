using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Eggplant.Entities.Persistence.SqlServer
{
	public class SqlServerPersistenceStore : PersistenceStore
	{
		public override PersistenceConnection Connect()
		{
			var innerConnection = new System.Data.SqlClient.SqlConnection(this.ConnectionString);
			innerConnection.Open();
			return new PersistenceConnection(innerConnection);
		}
	}
}
