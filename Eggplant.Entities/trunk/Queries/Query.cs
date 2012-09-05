using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eggplant.Entities.Model;
using Eggplant.Entities.Persistence;
using System.Data;

namespace Eggplant.Entities.Queries
{

	public class Query<T> : QueryBase
	{
		public QueryTemplate<T> Template { get; private set; }
		public List<Subquery> Subqueries { get; private set; }

		internal Query(QueryTemplate<T> template)
		{
			this.Template = template;
			this.Subqueries = new List<Subquery>();
		}

		public new InboundMappingContext<T> MappingContext
		{
			get { return (InboundMappingContext<T>)base.MappingContext; }
			internal set { base.MappingContext = value; }
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

		public new Query<T> Select(params IEntityProperty[] properties)
		{
			return (Query<T>) base.Select(properties);
		}

		public Query<T> Select(ICollectionProperty collection, Action<Subquery> subqueryInit)
		{
			this.SelectList.Add(collection);

			IInboundMapping collectionMapping;
			if (!this.MappingContext.SubMappings.TryGetValue(collection, out collectionMapping))
				throw new MappingException(String.Format("No inbound mapping defined for the collection property {0}.", collection.Name));

			SubqueryTemplate template;
			//try
			//{
			template = this.Template.SubqueryTemplates.First(subqueryTemplate => subqueryTemplate.ResultSet == collectionMapping.ResultSet);
			//}
			//catch (Exception ex)
			//{
			//}

			Subquery subquery = template.Start(this.Connection);
			subquery.MappingContext = (IInboundMappingContext)collectionMapping;

			this.Subqueries.Add(subquery);

			if (subqueryInit != null)
				subqueryInit(subquery);

			return this;
		}

		public Query<T> Filter(string filterExpression)
		{
			return (Query<T>)base.Filter(filterExpression);
		}

		public new Query<T> Sort(IEntityProperty property, SortOrder order)
		{
			return (Query<T>) base.Sort(property, order);
		}

		public new Query<T> Param(string name, object value, DbType? dbType = null, int? size = null)
		{
			return (Query<T>) base.Param(name, value, dbType, size);
		}

		public void Prepare()
		{
			var cmdText = new StringBuilder();

			foreach (Subquery subquery in this.Subqueries)
			{
			}
		}

		public IEnumerable<T> Execute(QueryExecutionMode mode)
		{
			throw new NotImplementedException();
		}

	}
}
