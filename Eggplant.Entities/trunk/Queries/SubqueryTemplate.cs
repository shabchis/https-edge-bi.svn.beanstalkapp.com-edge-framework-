using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using Eggplant.Entities.Model;
using Eggplant.Entities.Persistence;
using System.Text.RegularExpressions;
using System.IO;

namespace Eggplant.Entities.Queries
{
	public class SubqueryTemplate: QueryTemplateBase
	{
		public bool IsTopLevel { get; set; }
		public bool IsStandalone { get; set; }
		public QueryTemplate Template { get; internal set; }
		public int Index { get; set; }
		public string DataSet { get; set; }
		public string CommandText { get; set; }
		public Dictionary<string, SubqueryConditionalColumn> ConditionalColumns { get; private set; }

		public SubqueryTemplate(EntitySpace space): base(space)
		{
			this.ConditionalColumns = new Dictionary<string, SubqueryConditionalColumn>();
		}

		public SubqueryTemplate SetTopLevel(bool topLevel)
		{
			this.IsTopLevel = topLevel;
			return this;
		}

		public SubqueryTemplate SetStandalone(bool standalone)
		{
			this.IsStandalone = standalone;
			return this;
		}

		public SubqueryTemplate ConditionalColumn(string column, IEntityProperty mappedProperty)
		{
			return ConditionalColumn(column, column, mappedProperty);
		}

		public SubqueryTemplate ConditionalColumn(string columnAlias, string columnSyntax, IEntityProperty mappedProperty)
		{
			this.ConditionalColumns[columnAlias] = new SubqueryConditionalColumn()
			{
				ColumnAlias = columnAlias,
				ColumnSyntax = columnSyntax,
				Condition = subquery => subquery.SelectList.Count == 0 || subquery.SelectList.Contains(mappedProperty),
				MappedProperty = mappedProperty
			};

			return this;
		}

		public SubqueryTemplate ConditionalColumn(string column, Func<Subquery, bool> condition)
		{
			return ConditionalColumn(column, column, condition);
		}

		public SubqueryTemplate ConditionalColumn(string columnAlias, string columnSyntax, Func<Subquery, bool> condition)
		{
			this.ConditionalColumns[columnAlias] = new SubqueryConditionalColumn()
			{
				ColumnAlias = columnAlias,
				ColumnSyntax = columnSyntax,
				Condition = condition,
				MappedProperty = null
			};

			return this;
		}

		public new SubqueryTemplate DbParam(string name, object value, DbType? dbType = null, int? size = null)
		{
			base.DbParam(name, value, dbType, size);
			return this;
		}

		public new SubqueryTemplate DbParam(string name, Func<Query, object> valueFunc, DbType? dbType = null, int? size = null)
		{
			base.DbParam(name, valueFunc, dbType, size);
			return this;
		}

		internal Subquery Start(Query q)
		{
			Subquery subquery = new Subquery()
			{
				ParentQuery = q,
				Template = this
			};

			subquery.Connection = q.Connection;

			foreach (DbParameter parameter in this.DbParameters.Values)
				subquery.DbParameters.Add(parameter.Name, parameter.Clone());

			foreach (QueryParameter parameter in this.Parameters.Values)
				subquery.Parameters.Add(parameter.Name, parameter.Clone());

			return subquery;
		}
	}

	public class SubqueryConditionalColumn
	{
		public string ColumnAlias;
		public string ColumnSyntax;
		public IEntityProperty MappedProperty;
		public Func<Subquery, bool> Condition;
	}

}
