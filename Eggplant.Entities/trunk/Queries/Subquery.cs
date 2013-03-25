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
		public PersistenceAction PersistenceAction { get; internal set; }
		internal int ResultSetIndex { get; set; }

		internal Subquery(Query parent, SubqueryTemplate template, ISubqueryMapping mapping)
		{
			this.ParentQuery = parent;
			this.Template = template;
			this.Mapping = mapping;

			// Take persistence parameters from the subquery template only, but...
			foreach (PersistenceParameter parameter in template.PersistenceParameters.Values)
				this.PersistenceParameters.Add(parameter.Name, parameter.Clone());

			/*
			// Query parameters are inherited from the root query.
			foreach (QueryParameter parameter in template.Template.Parameters.Values)
				this.Parameters.Add(parameter.Name, parameter.Clone());
			*/

			// However, they might be overridden by the subquery template, so use the indexer here.
			foreach (QueryParameter parameter in template.Parameters.Values)
				this.Parameters[parameter.Name] = parameter.Clone();
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

		public new Subquery PersistenceParam(string name, object value, PersistenceParameterOptions options = null)
		{
			base.PersistenceParam(name, value, options);
			return this;
		}

		public Subquery PersistenceParam(string name, string fromQueryParam, Func<object,object> convertQueryParam = null, PersistenceParameterOptions options = null)
		{
			QueryParameter param = GetQueryParam(fromQueryParam);

			object val = param.Value;
			if (convertQueryParam != null)
				val = convertQueryParam(val);
			return this.PersistenceParam(name, val, options);
		}

		public Subquery PersistenceParamMap(IMapping mapToUse, string fromQueryParam)
		{
			QueryParameter param = GetQueryParam(fromQueryParam);
			return this.PersistenceParamMap(mapToUse, param.Value);
		}

		public Subquery PersistenceParamMap(IMapping mapToUse, object fromObject)
		{
			// TODO: add mapping direction to all param functions, dont' assume outbound
			MappingContext context = mapToUse.CreateContext(this.PersistenceAction.GetAdapter(PersistenceAdapterPurpose.Parameters, MappingDirection.Outbound), this);
			mapToUse.Apply(context);

			return this;
		}

		public new Subquery Param<V>(string paramName, V value)
		{
			ThrowIfRoot();
			base.Param<V>(paramName, value);
			return this;
		}

		private QueryParameter GetQueryParam(string paramName)
		{
			QueryParameter param;
			if (!this.Parameters.TryGetValue(paramName, out param))
				if (!this.ParentQuery.Parameters.TryGetValue(paramName, out param))
					throw new KeyNotFoundException(String.Format("Parameter '{0}' could not be found in either the query or the subquery.", paramName));
			return param;
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

			this.PersistenceAction = this.Template.PersistenceAction.Clone();
			this.IsPrepared = true;
		}

	}

}
