using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SqlClient;
using System.Data;

namespace Eggplant.Entities.Persistence.SqlServer
{
	public class SqlServerPersistenceStore : PersistenceStore
	{
		public override PersistenceConnection Connect()
		{
			var innerConnection = new System.Data.SqlClient.SqlConnection(this.ConnectionString);
			innerConnection.Open();
			return new PersistenceConnection(this, innerConnection);
		}

		public override System.Data.Common.DbCommand NewDbCommand(string commandText = null, System.Data.CommandType commandType = CommandType.Text)
		{
			return new SqlCommand(commandText) { CommandType = commandType };
		}

		public override System.Data.Common.DbParameter NewDbParameter(string name)
		{
			return new System.Data.SqlClient.SqlParameter(name, null);
		}

		public override PersistenceAdapter NewAdapter(System.Data.Common.DbDataReader reader)
		{
			return new SqlDataReaderAdapter((SqlDataReader)reader);
		}
	}
}
