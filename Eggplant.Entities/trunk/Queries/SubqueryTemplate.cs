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
		public string CommandText { get; set; }
		public Dictionary<string, SubqueryConditionalColumn> ConditionalColumns { get; private set; }
		internal List<Action<Subquery>> ActionsBefore = new List<Action<Subquery>>();
		internal List<Action<Subquery>> ActionsAfter = new List<Action<Subquery>>();

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

		public new SubqueryTemplate DbParamDefine(string name, object defaultValue, DbType? dbType = null, int? size = null)
		{
			base.DbParam(name, defaultValue, dbType, size);
			return this;
		}

		public SubqueryTemplate DbParamFromParam(string name, string sourceParamName, object nullValue = null, DbType? dbType = null, int? size = null)
		{
			this.DbParam(name, null, dbType, size);
			BeforeExecute(sq => sq.DbParamFromParam(name, sourceParamName, nullValue));
			return this;
		}

		public SubqueryTemplate DbParamsFromMap(IMapping mapToUse, object sourceValue)
		{
			this.BeforeExecute(sq => sq.DbParamsFromMap(mapToUse, sourceValue));
			return this;
		}

		public SubqueryTemplate DbParamsFromMap(IMapping mapToUse, string sourceParamName)
		{
			this.BeforeExecute(sq => sq.DbParamsFromMap(mapToUse, sourceParamName));
			return this;
		}

		public SubqueryTemplate BeforeExecute(Action<Subquery> action)
		{
			this.ActionsBefore.Add(action);
			return this;
		}

		public SubqueryTemplate AfterExecute(Action<Subquery> action)
		{
			this.ActionsAfter.Add(action);
			return this;
		}
	}

	public class SubqueryConditionalColumn
	{
		public string ColumnAlias;
		public string ColumnSyntax;
		public IEntityProperty MappedProperty;
		public Func<Subquery, bool> Condition;
	}

	public class SubqueryRelationship
	{
		public List<SubqueryRelationshipField> Fields;

		public SubqueryRelationship Field(string child, string parent)
		{
			if (Fields == null)
				Fields = new List<SubqueryRelationshipField>();

			this.Fields.Add(new SubqueryRelationshipField(child, parent));
			return this;
		}
	}

	public struct SubqueryRelationshipField
	{
		public string ChildField;
		public string ParentField;

		public SubqueryRelationshipField(string child, string parent)
		{
			ChildField = child;
			ParentField = parent;
		}
	}

}
