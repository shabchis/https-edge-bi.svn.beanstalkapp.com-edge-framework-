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

		protected List<SqlCommand> PreparedCommands { get; private set; }
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
			var subqueryNames = new List<string>();
			Action<IMapping> findSubqueryNames = null; findSubqueryNames = m =>
			{
				foreach (IMapping subMapping in m.SubMappings)
				{
					if (subMapping is ISubqueryMapping)
						subqueryNames.Add(((ISubqueryMapping)subMapping).SubqueryName);
					else
						findSubqueryNames(subMapping);
				}
			};
			findSubqueryNames(template.InboundMapping);

			// Create subquery objects from templates
			foreach (SubqueryTemplate subquerytpl in template.SubqueryTemplates)
			{
				if (!subqueryNames.Contains(subquerytpl.Name))
					continue;

				var subquery = new Subquery(this, subquerytpl);
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

		public void Prepare()
		{
			// ----------------------------------------
			// Prepare SQL commands
			var mainCommandText = new StringBuilder();
			SqlCommand mainCommand = new SqlCommand();
			this.PreparedCommands = new List<SqlCommand>();
			
			foreach (Subquery subquery in this.Subqueries)
			{
				if (!subquery.IsPrepared)
					subquery.Prepare();

				// Apply subquery SQL to command object
				if (subquery.Template.IsStandalone && !subquery.Template.IsRoot)
				{
					subquery.Command = new SqlCommand(subquery.PreparedCommandText);
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
					var p = new SqlParameter() { ParameterName = param.Name };// param.ValueFunction != null ? param.ValueFunction(this) : param.Value

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
				this.Prepare();

			var conn = (SqlConnection)this.Connection.DbConnection;

			foreach (SqlCommand command in this.PreparedCommands)
			{
				// Find associated subqueries
				Subquery[] subqueries = this.Subqueries.Where(s => s.Command == command).OrderBy(s => s.ResultSetIndex).ToArray();

				// Add parameters to the command object from all subqueries
				foreach(Subquery subquery in subqueries)
					foreach (DbParameter param in subquery.DbParameters.Values)
						command.Parameters[param.Name].Value = param.ValueFunction != null ? param.ValueFunction(this) : param.Value;

				//this.MappingContext = new MappingContext<T>(this, MappingDirection.Inbound);

				// Execute the command
				int resultSetIndex = -1;
				using (SqlDataReader reader = command.ExecuteReader())
				{
					// Iterate result set
					while (reader.NextResult())
					{
						resultSetIndex++;
						Subquery subquery = subqueries[resultSetIndex];

						while (reader.Read())
						{

						}
					}
				}
			}

			// Execute all commands
			throw new NotImplementedException();
		}

		
	}
}
