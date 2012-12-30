using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SqlClient;
using System.Data.Sql;

namespace Eggplant.Entities.Persistence.SqlServer
{
	public class SqlServerDataReaderChannel: PersistenceChannel
	{
		SqlDataReader _reader;

		public SqlServerDataReaderChannel(SqlDataReader reader)
		{
			_reader = reader;
		}

		public override object GetField(string field)
		{
			return _reader[field];
		}

		public override void SetField(string field, object value)
		{
			throw new InvalidOperationException("Cannot set field on a data reader.");
		}

		public override void Dispose()
		{
			_reader.Dispose();
		}
	}
}
