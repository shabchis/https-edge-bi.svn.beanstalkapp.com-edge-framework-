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
		public bool IsStandalone { get; set; }
		public QueryTemplate Template { get; internal set; }
		public string Name { get; set; }
		public PersistenceAction PersistenceAction { get; set; }
		public Dictionary<string, SubqueryConditionalColumn> ConditionalColumns { get; private set; }
		internal List<Action<Subquery>> DelegatesBefore = new List<Action<Subquery>>();
		internal List<Action<Subquery>> DelegatesAfter = new List<Action<Subquery>>();

		public SubqueryTemplate(EntitySpace space): base(space)
		{
			this.ConditionalColumns = new Dictionary<string, SubqueryConditionalColumn>();
		}

		public bool IsRoot
		{
			get { return this.Template.RootSubqueryTemplate == this; }
		}

		public SubqueryTemplate SetStandalone(bool standalone)
		{
			this.IsStandalone = standalone;
			return this;
		}

		public new SubqueryTemplate PersistenceParam(string name, object defaultValue, PersistenceParameterOptions options = null)
		{
			base.PersistenceParam(name, defaultValue, options);
			return this;
		}

		public SubqueryTemplate PersistenceParam(string name, string fromQueryParam, Func<object,object> convertQueryParam = null, PersistenceParameterOptions options = null)
		{
			this.PersistenceParam(name, null, options);
			BeforeExecute(sq => sq.PersistenceParam(name, fromQueryParam, convertQueryParam));
			return this;
		}

		public SubqueryTemplate PersistenceParamMap(IMapping mapToUse, object fromObject)
		{
			this.BeforeExecute(sq => sq.PersistenceParamMap(mapToUse, fromObject));
			return this;
		}

		public SubqueryTemplate PersistenceParamMap(IMapping mapToUse, string fromQueryParam)
		{
			this.BeforeExecute(sq => sq.PersistenceParamMap(mapToUse, fromQueryParam));
			return this;
		}

		public SubqueryTemplate BeforeExecute(Action<Subquery> action)
		{
			this.DelegatesBefore.Add(action);
			return this;
		}

		public SubqueryTemplate AfterExecute(Action<Subquery> action)
		{
			this.DelegatesAfter.Add(action);
			return this;
		}

		#region Condition columns - not fully implemented
		/*
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
		*/
		#endregion
	}

	public class SubqueryConditionalColumn
	{
		public string ColumnAlias;
		public string ColumnSyntax;
		public IEntityProperty MappedProperty;
		public Func<Subquery, bool> Condition;
	}

}
