using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eggplant.Entities.Model;
using Eggplant.Entities.Persistence;
using System.Data;

namespace Eggplant.Entities.Queries
{
	public abstract class Query : QueryBase
	{
		public List<Subquery> Subqueries { get; private set; }
		protected Dictionary<string, QueryArgument> Args;

		internal Query()
		{
			this.Subqueries = new List<Subquery>();
		}

		public override PersistenceConnection Connection
		{
			get { return base.Connection; }
			set
			{
				// Pass down connection to subqueries
				base.Connection = value;
				foreach (Subquery subquery in this.Subqueries)
					subquery.Connection = Connection;
			}
		}

		public V Argument<V>(string argumentName)
		{
			return (V)Args[argumentName].Value;
		}


	}

	public class Query<T> : Query
	{
		public QueryTemplate<T> Template { get; private set; }
		

		internal Query(QueryTemplate<T> template)
		{
			this.Template = template;

			if (template.Arguments != null && template.Arguments.Count > 0)
				Args = new Dictionary<string, QueryArgument>(template.Arguments);
		}

		public new MappingContext<T> MappingContext
		{
			get { return (MappingContext<T>)base.MappingContext; }
			internal set { base.MappingContext = value; }
		}


		public new Query<T> Select(params IEntityProperty[] properties)
		{
			return (Query<T>) base.Select(properties);
		}

		public Query<T> Select(ICollectionProperty collection, Action<Subquery> subqueryInit)
		{
			this.SelectList.Add(collection);

			IMapping collectionMapping;
			if (!this.MappingContext.SubMappings.TryGetValue(collection, out collectionMapping))
				throw new MappingException(String.Format("No inbound mapping defined for the collection property {0}.", collection.Name));

			SubqueryTemplate template;
			//try
			//{
			template = this.Template.SubqueryTemplates.First(subqueryTemplate => subqueryTemplate.DataSet == collectionMapping.DataSet);
			//}
			//catch (Exception ex)
			//{
			//}

			Subquery subquery = template.Start(this.Connection);
			subquery.MappingContext = (IMappingContext)collectionMapping;

			this.Subqueries.Add(subquery);

			if (subqueryInit != null)
				subqueryInit(subquery);

			return this;
		}

		public new Query<T> Filter(string filterExpression)
		{
			return (Query<T>)base.Filter(filterExpression);
		}

		public new Query<T> Sort(IEntityProperty property, SortOrder order)
		{
			return (Query<T>) base.Sort(property, order);
		}

		public Query<T> Argument<V>(string argumentName, V value)
		{
			QueryArgument arg;
			if (!Args.TryGetValue(argumentName, out arg))
				throw new KeyNotFoundException(String.Format("Argument '{0}' is not defined in the query template.", argumentName));
			if (!arg.ArgumentType.IsAssignableFrom(typeof(V)))
				throw new ArgumentException(String.Format("Argument '{0}' requires values of type {1}.", argumentName, arg.ArgumentType));
			
			arg.Value = value;
			return this;
		}

		public void Prepare()
		{
			var cmdText = new StringBuilder();

			foreach (Subquery subquery in this.Subqueries)
			{
				//subquery.pre
			}

			//this.IsPrepared = true;
		}

		public IEnumerable<T> Execute(QueryExecutionMode mode)
		{
			throw new NotImplementedException();
		}

	}
}
