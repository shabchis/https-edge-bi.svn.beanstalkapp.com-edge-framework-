using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eggplant.Entities.Persistence;
using Eggplant.Entities.Model;
using System.Data;

namespace Eggplant.Entities.Queries
{
	public abstract class QueryTemplate : QueryTemplateBase
	{
		public SubqueryTemplate RootSubqueryTemplate { get; set; }
		public List<SubqueryTemplate> SubqueryTemplates { get; private set; }
		
		internal QueryTemplate(EntitySpace space): base(space)
		{
			this.SubqueryTemplates = new List<SubqueryTemplate>();
		}
	}

	public class QueryTemplate<T> : QueryTemplate
	{
		public Mapping<T> InboundMapping;
		//public OutboundMapping<T> OutboundMapping;

		internal QueryTemplate(EntitySpace space):base(space)
		{
		}

		/// <summary>
		/// Creates a new empty query using this template.
		/// </summary>
		/// <returns></returns>
		public Query<T> Start()
		{
			var q = new Query<T>()
			{
				Template = this,
				EntitySpace = this.EntitySpace
			};
			q.MappingContext = new MappingContext<T>(q, this.InboundMapping, MappingDirection.Inbound);

			foreach (DbParameter parameter in this.DbParameters.Values)
				q.DbParameters.Add(parameter.Name, parameter.Clone());

			foreach (QueryParameter parameter in this.Parameters.Values)
				q.Parameters.Add(parameter.Name, parameter.Clone());

			// Possibly not needed because this is done by Select
			foreach (SubqueryTemplate subquerytpl in this.SubqueryTemplates)
			{
				Subquery subquery = subquerytpl.Start(q);
				q.Subqueries.Add(subquery);
			}

			return q;
		}



		public QueryTemplate<T> RootSubquery(string commandText, Action<SubqueryTemplate> inner = null)
		{
			SubqueryTemplate root = SubqueryInit(null, commandText, inner);
			if (root.Relationships.Count > 0)
				throw new QueryTemplateException("Root subquery cannot have any relationships.");

			this.RootSubqueryTemplate = root;
			return this;
		}

		public QueryTemplate<T> Subquery(string resultSetName, string commandText, Action<SubqueryTemplate> inner = null)
		{
			SubqueryInit(resultSetName, commandText, inner);
			return this;
		}

		private SubqueryTemplate SubqueryInit(string resultSetName, string commandText, Action<SubqueryTemplate> inner)
		{
			var subqueryTemplate = new SubqueryTemplate(this.EntitySpace)
			{
				ResultSetName = resultSetName,
				CommandText = commandText,
				Template = this
			};

			if (this.SubqueryTemplates.Any(p => p.ResultSetName == resultSetName))
				throw new QueryTemplateException("Cannot add subquery template with a result set name that is already included.");

			this.SubqueryTemplates.Add(subqueryTemplate);

			if (inner != null)
				inner(subqueryTemplate);

			return subqueryTemplate;
		}

		public new QueryTemplate<T> DbParam(string name, Func<Query, object> valueFunc, DbType? dbType = null, int? size = null)
		{
			base.DbParam(name, valueFunc, dbType, size);
			return this;
		}

		public new QueryTemplate<T> DbParam(string name, object value, DbType? dbType = null, int? size = null)
		{
			base.DbParam(name, value, dbType, size);
			return this;
		}

		public new QueryTemplate<T> Param<V>(string paramName, bool required = true, V defaultValue = default(V), V emptyValue = default(V))
		{
			base.Param<V>(paramName, required, defaultValue, emptyValue);
			return this;
		}

		
	}
}
