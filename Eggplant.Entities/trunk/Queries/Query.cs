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

		protected List<SqlCommand> PreparedCommands { get; private set; }
		private List<SubqueryExecutionData> ExecutionData;
		
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
			// Create execution list based on select list and active mappings
			this.ExecutionData = new List<SubqueryExecutionData>();
			PrepareExecutionData(null, this.MappingContext);

			// ----------------------------------------
			// Prepare SQL commands
			var mainCommandText = new StringBuilder();
			SqlCommand mainCommand = new SqlCommand();
			this.PreparedCommands = new List<SqlCommand>();
			
			foreach (SubqueryExecutionData execdata in this.ExecutionData)
			{
				if (!execdata.Subquery.IsPrepared)
					execdata.Subquery.Prepare();

				// Apply subquery SQL to command object
				if (execdata.Subquery.Template.IsStandalone && !execdata.Subquery.Template.IsRoot)
				{
					execdata.TargetCommand = new SqlCommand(execdata.Subquery.PreparedCommandText);
					this.PreparedCommands.Add(execdata.TargetCommand);
				}
				else
				{
					execdata.TargetCommand = mainCommand;

					mainCommandText.Append(execdata.Subquery.PreparedCommandText);
					if (!execdata.Subquery.PreparedCommandText.Trim().EndsWith(";"))
						mainCommandText.Append(";");
				}

				// Add parameters to the command object
				foreach (DbParameter param in execdata.Subquery.DbParameters.Values)
				{
					var p = new SqlParameter() { ParameterName = param.Name };// param.ValueFunction != null ? param.ValueFunction(this) : param.Value

					if (param.Size != null)
						p.Size = param.Size.Value;
					if (param.DbType != null)
						p.DbType = param.DbType.Value;

					// TODO: avoid exceptions on duplicate parameter names by prefixing them?
					execdata.TargetCommand.Parameters.Add(p);
				}

				// Add this subquery's relationships to its parent's 'Children' relation list for easy lookup during execution
				foreach (var relationship in execdata.Subquery.Template.Relationships)
				{
					SubqueryExecutionData parent = this.ExecutionData.Find(s => s.Subquery.Template == relationship.Key);
					if (parent.Children == null)
						parent.Children = new List<SubqueryRelationship>();

					parent.Children.Add(relationship.Value);
				}
			}

			// ----------------------------------------
			// Finalize the main command object
			mainCommand.CommandText = mainCommandText.ToString();
			this.PreparedCommands.Insert(0, mainCommand);

			this.IsPrepared = true;
		}

		// Recursive helper function
		void PrepareExecutionData(IEntityProperty property, IMapping mapping)
		{
			Subquery subquery = this.Subqueries.Find(s => s.Template.ResultSetName == mapping.ResultSetName);
			SubqueryExecutionData pass = this.ExecutionData.Find(p => p.Subquery == subquery);
			if (pass == null)
			{
				pass = new SubqueryExecutionData() { Subquery = subquery };
				this.ExecutionData.Add(pass);
			}

			if (property != null)
			{
				if (subquery.SelectList.Contains(property) && mapping.ResultSetName == subquery.Template.ResultSetName)
					pass.Mappings.Add(mapping);
			}

			// Current pass
			foreach (var pair in mapping.SubMappings)
				if (subquery.SelectList.Contains(pair.Key))
					PrepareExecutionData(pair.Key, pair.Value);
		}


		public IEnumerable<T> Execute(QueryExecutionMode mode)
		{
			if (!this.IsPrepared)
				this.Prepare();

			var conn = (SqlConnection)this.Connection.DbConnection;

			// Set all parameter values
			foreach (SubqueryExecutionData execdata in this.ExecutionData)
			{
				// Add parameters to the command object
				foreach (DbParameter param in execdata.Subquery.DbParameters.Values)
					execdata.TargetCommand.Parameters[param.Name].Value = param.ValueFunction != null ? param.ValueFunction(this) : param.Value;
			}

			// Execute all commands
			throw new NotImplementedException();
		}

		
	}

	internal class SubqueryExecutionData
	{
		public Subquery Subquery;
		public List<IMapping> Mappings = new List<IMapping>();
		public List<SubqueryRelationship> Children;
		public SqlCommand TargetCommand;

		//public Dictionary<Subquery, Dictionary<Identity, object>> ResultsCache; // for mappings with child relationships
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
