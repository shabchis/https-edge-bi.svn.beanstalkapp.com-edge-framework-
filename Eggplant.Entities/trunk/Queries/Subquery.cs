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

		internal void Prepare()
		{
			// .....................................
			// Columns

			var columns = new StringBuilder();
			int columnCount = 0;
			foreach (KeyValuePair<string, SubqueryConditionalColumn> columnCondition in this.Template.ConditionalColumns)
			{
				if (!columnCondition.Value.Condition(this))
					continue;

				// Add the column name
				columns.Append(columnCondition.Value.ColumnSyntax);

				columnCount++;
				if (columnCount < this.Template.ConditionalColumns.Count)
					columns.Append(", ");
			}

			// .....................................
			// Filters

			// TODO: filters

			// .....................................
			// Sorting

			// TODO: sorting

			// .....................................
			this.PreparedCommandText = this.Template.CommandText
				.Replace("{columns}", columns.ToString())
			;
			this.IsPrepared = true;
		}

	}

}
