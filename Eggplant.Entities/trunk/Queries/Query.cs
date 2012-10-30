using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eggplant.Entities.Model;
using Eggplant.Entities.Persistence;
using System.Data;
using System.Data.SqlClient;
using System.Collections;

namespace Eggplant.Entities.Queries
{
	public class Query<T> : Query
	{
		public QueryTemplate<T> Template { get; internal set; }
		
		internal Query()
		{
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
			template = this.Template.SubqueryTemplates.First(subqueryTemplate => subqueryTemplate.ResultSetName == collectionMapping.ResultSetName);
			//}
			//catch (Exception ex)
			//{
			//}

			Subquery subquery = template.Start(this);
			subquery.MappingContext = (IMappingContext)collectionMapping;

			this.Subqueries.Add(subquery);

			if (subqueryInit != null)
				subqueryInit(subquery);

			return this;
		}

		public new Query<T> Filter(params object[] filterExpression)
		{
			base.Filter(filterExpression);
			return this;
		}

		public new Query<T> Sort(IEntityProperty property, SortOrder order)
		{
			base.Sort(property, order);
			return this;
		}

		public new Query<T> Param<V>(string paramName, V value)
		{
			base.Param<V>(paramName, value);
			return this;
		}

		public void Prepare()
		{
			var cmdText = new StringBuilder();

			foreach (Subquery subquery in this.Subqueries)
			{
				if (!subquery.IsPrepared)
					subquery.Prepare();

				if (!subquery.Template.IsStandalone)
				{
					cmdText.Append(subquery.PreparedCommandText);
					if (!subquery.PreparedCommandText.Trim().EndsWith(";"))
						cmdText.Append(";");
				}
			}

			this.PreparedCommandText = cmdText.ToString();
			this.IsPrepared = true;
		}

		public IEnumerable<T> Execute(QueryExecutionMode mode)
		{
			if (!this.IsPrepared)
				this.Prepare();

			var conn = (SqlConnection)this.Connection.DbConnection;

			var commands = new List<SqlCommand>();
			var mainCommand = new SqlCommand(this.PreparedCommandText, conn);
			commands.Add(mainCommand);

			// Set all parameter values
			foreach (Subquery subquery in this.Subqueries)
			{
				// Choose the right command object
				SqlCommand cmd;
				if (subquery.Template.IsStandalone)
				{
					cmd = new SqlCommand(subquery.PreparedCommandText, conn);
					commands.Add(cmd);
				}
				else
					cmd = mainCommand;

				// Add parameters to the command object
				foreach (DbParameter param in subquery.DbParameters.Values)
				{
					var p = new SqlParameter(
						param.Name,
						param.ValueFunction != null ? param.ValueFunction(this) : param.Value
					);

					if (param.Size != null)
						p.Size = param.Size.Value;
					if (param.DbType != null)
						p.DbType = param.DbType.Value;

					cmd.Parameters.Add(p);
				}
			}

			// Setup caching for subquery relationships
			foreach(SubqueryTemplate subqueryTemplate in this.Template.SubqueryTemplates)
			{
				//subqueryTemplate.Relationships.Keys
			}

			var passes = new List<QueryExecutionMappingPass>();
			BuildMappingPasses(null, this.MappingContext, passes);

			// Execute all commands
			throw new NotImplementedException();
		}

		void BuildMappingPasses(IEntityProperty property, IMapping mapping, List<QueryExecutionMappingPass> passes)
		{
			Subquery subquery = this.Subqueries.Find(s => s.Template.ResultSetName == mapping.ResultSetName);
			QueryExecutionMappingPass pass = passes.Find(p => p.Subquery == subquery);
			if (pass == null)
			{
				pass = new QueryExecutionMappingPass() { Subquery = subquery };
				passes.Add(pass);
			}

			if (property != null)
			{
				if (subquery.SelectList.Contains(property) && mapping.ResultSetName == subquery.Template.ResultSetName)
					pass.Mappings.Add(mapping);
			}

			// Current pass
			foreach (var pair in mapping.SubMappings)
				if (subquery.SelectList.Contains(pair.Key))
					BuildMappingPasses(pair.Key, pair.Value, passes);
		}

	}

	internal class QueryExecutionMappingPass
	{
		public List<IMapping> Mappings = new List<IMapping>();
		public Subquery Subquery;
		public List<object> ResultsCache; // for mappings with child relationships
	}


	public abstract class Query : QueryBase
	{
		public List<Subquery> Subqueries { get; private set; }

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
	}


}
