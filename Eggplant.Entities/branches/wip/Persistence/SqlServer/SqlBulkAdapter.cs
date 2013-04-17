using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Data.SqlClient;

namespace Eggplant.Entities.Persistence.SqlServer
{
	public class SqlBulkAdapter: PersistenceAdapter
	{
		SqlBulkCopy _bulk;
		DataTable _buffer;

		internal SqlBulkAdapter(SqlPersistenceConnection connection, SqlBulkAction action) :base(connection, action)
		{
			_bulk = new SqlBulkCopy(connection.DbConnection, action.BulkCopyOptions, null)
			{
				BatchSize = action.BatchSize,
				DestinationTableName = action.TableName
			};

			_buffer = new DataTable("SqlBulkAdapter._buffer");
			foreach (PersistenceParameter param in action.Parameters.Values)
			{
				var options = (SqlPersistenceParameterOptions)param.Options;
				var tableCol = new DataColumn(param.Name);
				if (options.Size != null)
					tableCol.MaxLength = options.Size.Value;
				_buffer.Columns.Add(tableCol);
				_bulk.ColumnMappings.Add(new SqlBulkCopyColumnMapping(param.Name, param.Name));
			}
		}

		public override bool IsReusable
		{
			get { throw new NotImplementedException(); }
		}

		public override bool HasField(string field)
		{
			throw new NotImplementedException();
		}

		public override object GetField(string field)
		{
			throw new NotImplementedException();
		}

		public override void SetField(string field, object value)
		{
			throw new NotImplementedException();
		}

		public override bool HasParam(string param)
		{
			throw new NotImplementedException();
		}

		public override object GetParam(string param)
		{
			throw new NotImplementedException();
		}

		public override void SetParam(string param, object value)
		{
			throw new NotImplementedException();
		}

		public override void ExecuteAction()
		{
			// TODO: only execute if Flush should be called now

			throw new NotImplementedException();
		}

		public override bool NextResultSet()
		{
			throw new NotImplementedException();
		}

		public override bool NextResult()
		{
			throw new NotImplementedException();
		}

		public override void Dispose()
		{
			throw new NotImplementedException();
		}
	}
}
