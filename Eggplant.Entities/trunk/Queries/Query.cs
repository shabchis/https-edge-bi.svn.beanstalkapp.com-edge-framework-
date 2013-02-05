using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eggplant.Entities.Model;
using Eggplant.Entities.Persistence;
using System.Data;
using System.Collections;
using System.Data.Common;

namespace Eggplant.Entities.Queries
{
	public abstract class Query : QueryBase
	{
		public List<Subquery> Subqueries { get; private set; }

		internal Query()
		{
			this.Subqueries = new List<Subquery>();
		}

		public override PersistenceConnection Connection
		{
			get;
			internal set;
		}
	}

	public class Query<T> : Query
	{
		public QueryTemplate<T> Template { get; internal set; }

		protected List<DbCommand> PreparedCommands { get; private set; }
		protected int MainCommandResultSetCount { get; private set; }
		
		internal Query(QueryTemplate<T> template)
		{
			this.Template = template;
			this.EntitySpace = template.EntitySpace;

			foreach (DbParameter parameter in template.DbParameters.Values)
				this.DbParameters.Add(parameter.Name, parameter.Clone());

			foreach (QueryParameter parameter in template.Parameters.Values)
				this.Parameters.Add(parameter.Name, parameter.Clone());

			// Find relevant subqueries
			var subqueryNames = new Dictionary<string, ISubqueryMapping>();
			Action<IMapping> findSubqueryNames = null; findSubqueryNames = m =>
			{
				foreach (IMapping subMapping in m.SubMappings)
				{
					if (subMapping is ISubqueryMapping)
						subqueryNames.Add(((ISubqueryMapping)subMapping).SubqueryName, (ISubqueryMapping)subMapping);
					else
						findSubqueryNames(subMapping);
				}
			};
			findSubqueryNames(template.Mapping);

			// Create subquery objects from templates
			foreach (SubqueryTemplate subquerytpl in template.SubqueryTemplates)
			{
				ISubqueryMapping mapping = null;
				if (subquerytpl.IsRoot)
					mapping = (ISubqueryMapping) template.Mapping;
				else if (!subqueryNames.TryGetValue(subquerytpl.Name, out mapping))
					continue;

				var subquery = new Subquery(this, subquerytpl, mapping);
				this.Subqueries.Add(subquery);
			}
		}

		public new MappingContext<T> MappingContext
		{
			get { return (MappingContext<T>)base.MappingContext; }
			protected set { base.MappingContext = value; }
		}


		public new Query<T> Select(params IEntityProperty[] properties)
		{
			return (Query<T>) base.Select(properties);
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

		public void Prepare(PersistenceStore store)
		{
			// ----------------------------------------
			// Prepare SQL commands
			var mainCommandText = new StringBuilder();
			DbCommand mainCommand = store.NewDbCommand();
			this.PreparedCommands = new List<DbCommand>();
			
			foreach (Subquery subquery in this.Subqueries)
			{
				if (!subquery.IsPrepared)
					subquery.Prepare();

				// Apply subquery SQL to command object
				if (subquery.Template.IsStandalone && !subquery.Template.IsRoot)
				{
					subquery.Command = store.NewDbCommand(subquery.PreparedCommandText);
					this.PreparedCommands.Add(subquery.Command);
				}
				else
				{
					subquery.Command = mainCommand;

					mainCommandText.Append(subquery.PreparedCommandText);
					if (!subquery.PreparedCommandText.Trim().EndsWith(";"))
						mainCommandText.Append(";");

					this.MainCommandResultSetCount++;
					subquery.ResultSetIndex = MainCommandResultSetCount;
				}

				// Add parameters to the command object
				foreach (DbParameter param in subquery.DbParameters.Values)
				{
					System.Data.Common.DbParameter p = store.NewDbParameter(param.Name);// param.ValueFunction != null ? param.ValueFunction(this) : param.Value

					if (param.Size != null)
						p.Size = param.Size.Value;
					if (param.DbType != null)
						p.DbType = param.DbType.Value;

					// TODO: avoid exceptions on duplicate parameter names by prefixing them?
					subquery.Command.Parameters.Add(p);
				}
			}

			// ----------------------------------------
			// Finalize the main command object
			mainCommand.CommandText = mainCommandText.ToString();
			this.PreparedCommands.Insert(0, mainCommand);

			this.IsPrepared = true;
		}

		public Query<T> Connect(PersistenceConnection connection = null)
		{
			if (connection == null)
			{
				connection = PersistenceStore.ThreadConnection;

				if (connection == null)
					throw new ArgumentNullException("There is no active thread connection - a valid connection object must be supplied.", "connection");
			}

			this.Connection = connection;

			return this;
		}

		public IEnumerable<T> Execute(QueryExecutionMode mode)
		{
			if (this.Connection == null)
			{
				try { Connect(); }
				catch (ArgumentNullException) { throw new QueryExecutionException("There is no active thread connection - Connect must be called with a valid connection object before Execute."); }
			}

			if (!this.IsPrepared)
				this.Prepare(this.Connection.Store);

			DbConnection conn = this.Connection.DbConnection;

			foreach (DbCommand command in this.PreparedCommands)
			{
				command.Connection = conn;

				// Find associated subqueries
				Subquery[] subqueries = this.Subqueries.Where(s => s.Command == command).OrderBy(s => s.ResultSetIndex).ToArray();

				// Add parameters to the command object from all subqueries
				foreach(Subquery subquery in subqueries)
					foreach (DbParameter param in subquery.DbParameters.Values)
						command.Parameters[param.Name].Value = param.ValueFunction != null ? param.ValueFunction(this) : param.Value;

				// Execute the command
				int resultSetIndex = -1;
				using (DbDataReader reader = command.ExecuteReader())
				{
					PersistenceAdapter adapter = this.Connection.Store.NewAdapter(reader);

					// TODO: Iterate result set
					//do
					//{
						resultSetIndex++;
						Subquery subquery = subqueries[resultSetIndex];

						MappingContext context = subquery.Mapping.CreateContext(adapter);
						foreach (T result in subquery.Mapping.ApplyAndReturn(context))
							yield return result;
					//}
					//while (reader.NextResult());
				}
			}
		}		
	}
}
