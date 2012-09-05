using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using Eggplant2.Model;

namespace Eggplant2.Queries
{
	public class Subquery : QueryBase
	{
		public SubqueryTemplate Template { get; private set; }

		internal Subquery()
		{
		}

		public new Subquery Select(params IEntityProperty[] properties)
		{
			return (Subquery)base.Select(properties);
		}

		public new Subquery Filter(params object[] filter)
		{
			return (Subquery)base.Filter(filter);
		}

		public new Subquery Sort(IEntityProperty property, SortOrder order)
		{
			return (Subquery)base.Sort(property, order);
		}

		public new Subquery Param(string name, object value, DbType? dbType = null, int? size = null)
		{
			return (Subquery)base.Param(name, value, dbType, size);
		}
	}

}
