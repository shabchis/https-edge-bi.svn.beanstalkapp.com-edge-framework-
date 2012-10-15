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
		public Dictionary<string, SubqueryParameter> Parameters { get; private set; }

		internal Subquery()
		{
			this.Parameters = new Dictionary<string, SubqueryParameter>();
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

		public Subquery Param(string name, object value, DbType? dbType = null, int? size = null)
		{
			SubqueryParameter param;
			if (dbType != null || size != null || !this.Parameters.TryGetValue(name, out param))
			{
				this.Parameters[name] = param = new SubqueryParameter()
				{
					Name = name,
					DbType = dbType,
					Size = size
				};
			}
			param.Value = value;

			return this;
		}

		protected void Prepare()
		{
			// .....................................
			// Columns

			var columns = new StringBuilder();
			int columnCount = 0;
			foreach (var condition in this.Template.Columns)
			{
				if (!condition.Value(this))
					continue;

				// Add the column name
				columns.Append(condition.Key);

				columnCount++;
				if (columnCount < this.Template.Columns.Count)
					columns.Append(", ");
			}

			// .....................................
			// Filters

			// TODO: filters
		}
	}

}
