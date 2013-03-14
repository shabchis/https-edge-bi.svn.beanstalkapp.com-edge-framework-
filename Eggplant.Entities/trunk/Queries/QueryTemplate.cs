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



		public QueryTemplate<T> RootSubquery(string commandText, Action<SubqueryTemplate> inner = null)
		{
			SubqueryTemplate root = SubqueryInit(null, commandText, inner);
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
				Name = resultSetName,
				CommandText = commandText,
				Template = this
			};

			if (this.SubqueryTemplates.Any(p => p.Name == resultSetName))
				throw new QueryTemplateException("Cannot add subquery template with a result set name that is already included.");

			this.SubqueryTemplates.Add(subqueryTemplate);

			if (inner != null)
				inner(subqueryTemplate);

			return subqueryTemplate;
		}

		public new QueryTemplate<T> Param<V>(string paramName, bool required = true, V defaultValue = default(V), V emptyValue = default(V))
		{
			base.Param<V>(paramName, required, defaultValue, emptyValue);
			return this;
		}

		
	}
}
