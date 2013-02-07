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

		public override bool HasField(string field)
		{
			for (int i = 0; i < _reader.FieldCount; i++)
				if (_reader.GetName(i) == field)
					return true;

			return false;
		}

		public override object GetField(string field)
		{
			try
			{
				object val = _reader[field];
				if (val is DBNull)
					val = null;
				return val;
			}
			catch (IndexOutOfRangeException ex)
			{
				throw new MappingException(String.Format("Field '{0}' not preset in the SQL results.", field), ex);
			}
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
