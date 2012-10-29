using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using Eggplant.Entities.Model;
using Eggplant.Entities.Persistence;

namespace Eggplant.Entities.Queries
{
	public class Subquery : QueryBase
	{
		public Query ParentQuery { get; internal set; }
		public SubqueryTemplate Template { get; internal set; }

		internal Subquery()
		{
		}

		private void ThrowIfTopLevel()
		{
			if (this.Template.IsTopLevel)
				throw new InvalidOperationException("Top level subtemplate receives the select, filter and sort lists from the main query.");
		}

		public new Subquery Select(params IEntityProperty[] properties)
		{
			ThrowIfTopLevel();
			return (Subquery)base.Select(properties);
		}

		public new Subquery Filter(params object[] filterExpression)
		{
			ThrowIfTopLevel();
			return (Subquery)base.Filter(filterExpression);
		}

		public new Subquery Sort(IEntityProperty property, SortOrder order)
		{
			ThrowIfTopLevel();
			return (Subquery)base.Sort(property, order);
		}

		public new Subquery DbParam(string name, object value, DbType? dbType = null, int? size = null)
		{
			ThrowIfTopLevel();
			base.DbParam(name, value, dbType, size);
			return this;
		}

		public new Subquery Param<V>(string paramName, V value)
		{
			ThrowIfTopLevel();
			base.Param<V>(paramName, value);
			return this;
		}

		internal void Prepare()
		{
			// .....................................
			// If top level, inherit all selects, filters, sorting
			this.SelectList.Clear();
			this.SelectList.AddRange(this.ParentQuery.SelectList);
			this.FilterExpression = this.ParentQuery.FilterExpression;
			this.SortingList.Clear();
			this.SortingList.AddRange(this.ParentQuery.SortingList);

			// .....................................
			// Columns

			var columns = new StringBuilder();
			var expressionParts = new List<SubqueryConditionalColumn>();
			foreach (KeyValuePair<string, SubqueryConditionalColumn> columnCondition in this.Template.ConditionalColumns)
			{
				if (!columnCondition.Value.Condition(this))
					continue;

				expressionParts.Add(columnCondition.Value);
			}
			for (int i = 0; i < expressionParts.Count; i++)
			{
				SubqueryConditionalColumn columnCondition = expressionParts[i];

				// Add the column name
				columns.Append(columnCondition.ColumnSyntax);
				columns.Append(" as ");
				columns.Append(columnCondition.ColumnAlias);

				if (i < expressionParts.Count-1)
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
				.Replace("{filter}", string.Empty)
				.Replace("{sorting}", string.Empty);
			;
			this.IsPrepared = true;
		}

	}

}
