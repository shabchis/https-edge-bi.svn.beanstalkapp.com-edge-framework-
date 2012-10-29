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
		public Query<T> Start(PersistenceConnection connection)
		{
			if (connection == null)
				throw new ArgumentNullException("connection");

			var q = new Query<T>()
			{
				Template = this,
				Connection = connection,
				EntitySpace = this.EntitySpace,
				MappingContext = new MappingContext<T>(this.InboundMapping, MappingDirection.Inbound, connection)
			};

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

		public QueryTemplate<T> Subquery(string dataSet, string commandText, Action<SubqueryTemplate> inner = null)
		{
			return this.Subquery(dataSet, -1, commandText, inner);
		}

		public QueryTemplate<T> Subquery(string dataSet, int index, string commandText, Action<SubqueryTemplate> inner = null)
		{
			var subqueryTemplate = new SubqueryTemplate(this.EntitySpace)
			{
				DataSet = dataSet,
				Index = index < 0 ? this.SubqueryTemplates.Count : index,
				CommandText = commandText
			};

			if (index >= 0)
				this.SubqueryTemplates.Insert(index, subqueryTemplate);
			else
				this.SubqueryTemplates.Add(subqueryTemplate);

			// Activate inner only for the command
			if (inner != null)
				inner(subqueryTemplate);

			return this;
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
