using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SqlClient;

namespace Eggplant.Entities.Persistence.SqlServer
{
	public class SqlBulkAction: PersistenceAction
	{
		public string TableName {get; set;}
		public int BatchSize {get; set;}
		public SqlBulkCopyOptions BulkCopyOptions { get; set; }

		public SqlBulkAction(string tableName, int batchSize, SqlBulkCopyOptions bulkCopyOptions = SqlBulkCopyOptions.Default)
		{
			this.TableName = tableName;
			this.BatchSize = batchSize;
			this.BulkCopyOptions = bulkCopyOptions;
		}

		public override bool IsAppendable
		{
			get { return false; }
		}

		protected override void OnAppend(PersistenceAction action)
		{
			throw new NotSupportedException("Appending bulk actions is not supported.");
		}

		public override PersistenceAction Clone()
		{
			return new SqlBulkAction(this.TableName, this.BatchSize, this.BulkCopyOptions);
		}

		public override PersistenceAdapter GetAdapter()
		{
			return new SqlBulkAdapter(null, this);
		}
	}
}
