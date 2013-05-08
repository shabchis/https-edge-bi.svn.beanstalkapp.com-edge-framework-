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
		public Mapping<T> Mapping;
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
			return new Query<T>(this);
		}

		public QueryTemplate<T> RootSubquery(PersistenceAction action, Action<SubqueryTemplate> inner = null)
		{
			SubqueryTemplate root = SubqueryInit(null, action, inner, true);
			this.RootSubqueryTemplate = root;
			return this;
		}

		public QueryTemplate<T> Subquery(string subqueryName, PersistenceAction action, Action<SubqueryTemplate> inner = null, bool deferred = false)
		{
			SubqueryInit(subqueryName, action, inner, deferred);
			return this;
		}

		private SubqueryTemplate SubqueryInit(string subqueryName, PersistenceAction action, Action<SubqueryTemplate> inner, bool deferred)
		{
			var subqueryTemplate = new SubqueryTemplate(this.EntitySpace)
			{
				Name = subqueryName,
				PersistenceAction = action,
				Template = this,
				IsDeferred = deferred
			};

			if (this.SubqueryTemplates.Any(p => p.Name == subqueryName))
				throw new QueryTemplateException("A subquery with the same name is already included in the query template.");

			this.SubqueryTemplates.Add(subqueryTemplate);

			if (inner != null)
				inner(subqueryTemplate);

			return subqueryTemplate;
		}

		public new QueryTemplate<T> Input<V>(string inputName, bool required = true, V defaultValue = default(V), V emptyValue = default(V))
		{
			base.Input<V>(inputName, required, defaultValue, emptyValue);
			return this;
		}

		
	}
}
