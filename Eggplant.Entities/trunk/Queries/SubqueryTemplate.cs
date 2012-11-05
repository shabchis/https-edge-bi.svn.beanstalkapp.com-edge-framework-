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
		public string ResultSetName { get; set; }
		public string CommandText { get; set; }
		public Dictionary<string, SubqueryConditionalColumn> ConditionalColumns { get; private set; }
		public Dictionary<SubqueryTemplate, SubqueryRelationship> Relationships { get; private set; }

		public SubqueryTemplate(EntitySpace space): base(space)
		{
			this.ConditionalColumns = new Dictionary<string, SubqueryConditionalColumn>();
			this.Relationships = new Dictionary<SubqueryTemplate, SubqueryRelationship>();
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

		public SubqueryTemplate RootRelationship(Action<SubqueryRelationship> relationshipInit)
		{
			return Relationship(null, relationshipInit);
		}

		public SubqueryTemplate Relationship(string resultSetName, Action<SubqueryRelationship> relationshipInit)
		{
			var relationship = new SubqueryRelationship();
			relationshipInit(relationship);
			this.Relationships.Add(this.Template.SubqueryTemplates.Find(subtpl => subtpl.ResultSetName == resultSetName), relationship);
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
