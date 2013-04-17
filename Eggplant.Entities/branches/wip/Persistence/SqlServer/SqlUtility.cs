using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;

namespace Eggplant.Entities.Persistence.SqlServer
{
	public static class SqlUtility
	{
		public static SqlPersistenceParameterOptions ParamOptions(SqlDbType? dbType = null, int? size = null)
		{
			return new SqlPersistenceParameterOptions() { SqlDbType = dbType, Size = size };
		}
	}
}
