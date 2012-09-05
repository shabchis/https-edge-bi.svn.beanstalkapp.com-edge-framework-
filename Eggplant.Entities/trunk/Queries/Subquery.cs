using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using Eggplant.Entities.Model;

namespace Eggplant.Entities.Queries
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

		public new Subquery Filter(string filterExpression)
		{
			return (Subquery)base.Filter(filterExpression);
		}

		public new Subquery Sort(IEntityProperty property, SortOrder order)
		{
			return (Subquery)base.Sort(property, order);
		}

		public new Subquery Column(string placeHolder, IEntityProperty property)
		{
			return (Subquery)base.Column(placeHolder, property);
		}

		public new Subquery Param(string name, object value, DbType? dbType = null, int? size = null)
		{
			return (Subquery)base.Param(name, value, dbType, size);
		}
	}

}
