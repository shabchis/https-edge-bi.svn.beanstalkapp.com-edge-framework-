using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SqlClient;
using System.Data.Sql;

namespace Eggplant.Entities.Persistence.SqlServer
{
	public class SqlDataReaderAdapter: PersistenceAdapter
	{
		SqlDataReader _reader;

		public SqlDataReaderAdapter(SqlDataReader reader)
		{
			_reader = reader;
		}

		public override object GetField(string field)
		{
			object val = _reader[field];
			if (val is DBNull)
				val = null;
			return val;
		}

		public override void SetField(string field, object value)
		{
			throw new InvalidOperationException("Cannot set field on a data reader.");
		}

		public override void Dispose()
		{
			_reader.Dispose();
		}

		public override bool Read()
		{
			return _reader.Read();
		}
	}
}
