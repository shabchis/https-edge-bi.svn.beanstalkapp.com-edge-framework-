using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Eggplant.Entities.Persistence.SqlServer
{
	public class SqlParameterAdapter: PersistenceAdapter
	{
		public new SqlPersistenceAction Action
		{
			get { return (SqlPersistenceAction)base.Action; }
		}

		public SqlParameterAdapter(SqlPersistenceAction action, MappingDirection mappingDirection)
			: base(action, mappingDirection)
		{
		}

		public override bool HasField(string field)
		{
			return this.Action.Command.Parameters.Contains(field);
		}

		public override object GetField(string field)
		{
			return this.Action.Command.Parameters[field].Value;
		}

		public override void SetField(string field, object value)
		{
			this.Action.Command.Parameters[field].Value = value;
		}

		public override void Dispose()
		{
		}

		public override bool NextResultSet()
		{
			throw new InvalidOperationException();
		}

		public override bool NextResult()
		{
			throw new InvalidOperationException();
		}
	}
}
