using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SqlClient;

namespace Eggplant.Entities.Persistence.SqlServer
{
	public class SqlBulkCommand: PersistenceCommand
	{
		public string TableName {get; set;}
		public int BatchSize {get; set;}
		public SqlBulkCopyOptions BulkCopyOptions { get; set; }

		public SqlBulkCommand(string tableName, int batchSize, SqlBulkCopyOptions bulkCopyOptions = SqlBulkCopyOptions.Default)
		{
			this.TableName = tableName;
			this.BatchSize = batchSize;
			this.BulkCopyOptions = bulkCopyOptions;
		}

		public override bool IsAppendable
		{
			get { return false; }
		}

		protected override void OnAppend(PersistenceCommand command)
		{
			throw new NotSupportedException("Appending bulk commands is not supported.");
		}

		public override PersistenceCommand Clone()
		{
			return new SqlBulkCommand(this.TableName, this.BatchSize, this.BulkCopyOptions);
		}

		public override PersistenceAdapter GetAdapter(PersistenceConnection connection)
		{
			return new SqlBulkAdapter((SqlPersistenceConnection)connection, this);
		}
	}
}
