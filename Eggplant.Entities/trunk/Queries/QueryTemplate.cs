using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eggplant.Entities.Persistence;
using Eggplant.Entities.Model;
using System.Data;

namespace Eggplant.Entities.Queries
{
	public abstract class QueryTemplate
	{
		public EntitySpace EntitySpace { get; private set; }
		public List<SubqueryTemplate> SubqueryTemplates { get; private set; }
		public SubqueryTemplate DefaultSubqueryTemplate { get; set; }
		public Dictionary<string, QueryArgument> Arguments { get; private set;  }

		internal QueryTemplate(EntitySpace space)
		{
			this.EntitySpace = space;
			this.SubqueryTemplates = new List<SubqueryTemplate>();
			this.Arguments = new Dictionary<string, QueryArgument>();
		}
	}

	public class QueryArgument
	{
		public string Name;
		public object Value;
		public Type ArgumentType;
		public bool IsRequired;
		public object DefaultValue;
		public object EmptyValue;
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
		public Query<T> Start(PersistenceConnection connection = null)
		{
			return new Query<T>(this)
			{
				Connection = connection,
				EntitySpace = this.EntitySpace,
				MappingContext = new MappingContext<T>(this.InboundMapping, MappingDirection.Inbound, connection)
			};
		}

		public QueryTemplate<T> Subquery(string dataSet, string commandText, Action<SubqueryTemplate> inner = null)
		{
			return this.Subquery(dataSet, -1, commandText, inner);
		}

		public QueryTemplate<T> Subquery(string dataSet, int index, string commandText, Action<SubqueryTemplate> inner = null)
		{
			var subqueryTemplate = new SubqueryTemplate()
			{
				DataSet = dataSet,
				Index = index < 0 ? this.SubqueryTemplates.Count : index,
				CommandText = ParseText(commandText)
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

		private static string ParseText(string commandText)
		{
			const string columnFinderPattern = @"\bas\s+(?<column>[a-zA-Z_]\w*)?\s*((--\s*#\s*COLUMN\s*$)|(\/\*\s*#\s*COLUMN\s*\*\/))";
			throw new NotImplementedException();
		}

		public QueryTemplate<T> DefaultSubquery(string dataSet)
		{
			this.DefaultSubqueryTemplate = this.SubqueryTemplates.Find(t => t.DataSet == dataSet);
			return this;
		}

		public QueryTemplate<T> DefaultSubquery(SubqueryTemplate subqueryTemplate)
		{
			if (!this.SubqueryTemplates.Contains(subqueryTemplate))
				throw new ArgumentException("Subquery was be added to the the SubqueryTemplates list first.", "subqueryTemplate");

			this.DefaultSubqueryTemplate = subqueryTemplate;
			return this;
		}

		public QueryTemplate<T> Argument<V>(string argumentName, bool required = true, V defaultValue = default(V), V emptyValue = default(V))
		{
			this.Arguments[argumentName] = new QueryArgument()
			{
				Name = argumentName,
				ArgumentType = typeof(V),
				IsRequired = required,
				DefaultValue = defaultValue,
				EmptyValue = emptyValue
			};

			return this;
		}
	}
}
