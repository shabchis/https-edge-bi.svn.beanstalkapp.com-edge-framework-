using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using Eggplant.Entities.Model;
using Eggplant.Entities.Persistence;
using System.Data.Common;
//using System.Data.SqlClient;

namespace Eggplant.Entities.Queries
{
	public class Subquery : QueryBase
	{
		public Query ParentQuery { get; private set; }
		public SubqueryTemplate Template { get; private set; }
		public ISubqueryMapping Mapping { get; private set; }
		public string PreparedCommandText { get; protected set; }
		
		internal DbCommand Command { get; set; }
		internal int ResultSetIndex { get; set; }

		internal Subquery(Query parent, SubqueryTemplate template, ISubqueryMapping mapping)
		{
			this.ParentQuery = parent;
			this.Template = template;
			this.Mapping = mapping;

			foreach (DbParameter parameter in template.DbParameters.Values)
				this.DbParameters.Add(parameter.Name, parameter.Clone());

			foreach (QueryParameter parameter in template.Parameters.Values)
				this.Parameters.Add(parameter.Name, parameter.Clone());
		}

		public override PersistenceConnection Connection
		{
			get { return this.ParentQuery.Connection; }
			internal set { throw new NotSupportedException("Subquery connection cannot be set directly and must use the parent query's connection."); }
		}

		private void ThrowIfRoot()
		{
			if (this.Template.IsRoot)
				throw new InvalidOperationException("Root subtemplate receives the select, filter and sort lists from the main query.");
		}

		public new Subquery Select(params IEntityProperty[] properties)
		{
			ThrowIfRoot();
			return (Subquery)base.Select(properties);
		}

		public new Subquery Filter(params object[] filterExpression)
		{
			ThrowIfRoot();
			return (Subquery)base.Filter(filterExpression);
		}

		public new Subquery Sort(IEntityProperty property, SortOrder order)
		{
			ThrowIfRoot();
			return (Subquery)base.Sort(property, order);
		}

		public new Subquery DbParam(string name, object value, DbType? dbType = null, int? size = null)
		{
			ThrowIfRoot();
			base.DbParam(name, value, dbType, size);
			return this;
		}

		public new Subquery Param<V>(string paramName, V value)
		{
			ThrowIfRoot();
			base.Param<V>(paramName, value);
			return this;
		}

		internal void Prepare()
		{
			/*
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
			*/

			this.PreparedCommandText = this.Template.CommandText;
			this.IsPrepared = true;
		}

	}

}
