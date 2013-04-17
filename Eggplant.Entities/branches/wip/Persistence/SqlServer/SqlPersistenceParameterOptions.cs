using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;

namespace Eggplant.Entities.Persistence.SqlServer
{
	public class SqlPersistenceParameterOptions : PersistenceParameterOptions
	{
		public SqlDbType? SqlDbType;
		public int? Size;

		public override PersistenceParameterOptions Clone()
		{
			return new SqlPersistenceParameterOptions()
			{
				SqlDbType = this.SqlDbType,
				Size = this.Size
			};
		}

		public override bool Equals(object obj)
		{
			if (!(obj is SqlPersistenceParameterOptions))
				return false;

			var other = (SqlPersistenceParameterOptions)obj;
			return
				other.SqlDbType == this.SqlDbType &&
				other.Size == this.Size;

		}
	}
}
