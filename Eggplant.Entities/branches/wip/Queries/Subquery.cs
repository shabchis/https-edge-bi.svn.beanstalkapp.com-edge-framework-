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
		internal int InboundSetIndex { get; set; }

		internal Subquery(Query parent, SubqueryTemplate template, ISubqueryMapping mapping)
		{
			this.ParentQuery = parent;
			this.Template = template;
			this.Mapping = mapping;

			// However, they might be overridden by the subquery template, so use the indexer here.
			foreach (QueryInput parameter in template.Inputs.Values)
				this.Inputs[parameter.Name] = parameter.Clone();
		}

		public override PersistenceConnection Connection
		{
			get { return this.ParentQuery.Connection; }
			internal set { throw new NotSupportedException("Subquery connection cannot be set directly and must use the parent query's connection."); }
		}

		private void ThrowIfRoot()
		{
			if (this.Template.IsRoot)
				throw new InvalidOperationException("Root subquery receives the input, select, filter and sort lists from the main query.");
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

		public object Param(string name)
		{
			return this.PersistenceAction.Parameters[name].Value;
		}

		public Subquery Param(string name, object value)
		{
			this.PersistenceAction.Parameters[name].Value = value;
			return this;
		}

		public new Subquery Input<V>(string inputName, V value)
		{
			ThrowIfRoot();
			base.Input<V>(inputName, value);
			return this;
		}

		internal QueryInput GetQueryInput(string inputName)
		{
			QueryInput param;
			if (!this.Inputs.TryGetValue(inputName, out param))
				if (!this.ParentQuery.Inputs.TryGetValue(inputName, out param))
					throw new KeyNotFoundException(String.Format("Input '{0}' could not be found in either the query or the subquery.", inputName));
			return param;
		}

		internal void Prepare()
		{
			// TODO: columns
			// TODO: filters
			// TODO: sorting

			this.PersistenceAction = this.Template.PersistenceAction.Clone();
			this.IsPrepared = true;
		}

	}

}
