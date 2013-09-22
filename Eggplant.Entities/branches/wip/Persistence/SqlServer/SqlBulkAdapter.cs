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

		internal SqlBulkAdapter(SqlPersistenceConnection connection, SqlBulkCommand action) :base(connection, action)
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

		public override void Begin()
		{
			throw new NotImplementedException();
		}

		public override void End()
		{
			throw new NotImplementedException();
		}

		public override bool HasOutboundField(string field)
		{
			throw new NotImplementedException();
		}

		public override object GetOutboundField(string field)
		{
			throw new NotImplementedException();
		}

		public override void SetOutboundField(string field, object value)
		{
			throw new NotImplementedException();
		}

		public override void NewOutboundRow()
		{
			throw new NotImplementedException();
		}

		public override bool SubmitOutboundRow()
		{
			throw new NotImplementedException();
		}

		public override bool NextInboundSet()
		{
			throw new NotImplementedException();
		}

		public override bool NextInboundRow()
		{
			throw new NotImplementedException();
		}

		public override int InboundSetIndex
		{
			get { throw new NotImplementedException(); }
		}

		public override bool HasInboundField(string field)
		{
			throw new NotImplementedException();
		}

		public override object GetInboundField(string field)
		{
			throw new NotImplementedException();
		}
	}
}
