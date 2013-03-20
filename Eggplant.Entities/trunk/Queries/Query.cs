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

		protected List<PersistenceAction> PersistenceActions { get; private set; }
		private int _resultSetCount = 0;
		private IEnumerable _batchSource = null;
		private Action<object> _batchAction = null;
		
		internal Query(QueryTemplate<T> template)
		{
			this.Template = template;
			this.EntitySpace = template.EntitySpace;

			foreach (PersistenceParameter parameter in template.PersistenceParameters.Values)
			{
				this.PersistenceParameters.Add(parameter.Name, parameter.Clone());
			}

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

		public Query<T> Batch<ItemT>(IEnumerable<ItemT> batchSource, string paramName)
		{
			return this.Batch<ItemT>(batchSource, (q, item) => q.Param<ItemT>(paramName, item));
		}

		public Query<T> Batch<ItemT>(IEnumerable<ItemT> batchSource, Action<Query<T>, ItemT> batchAction)
		{
			if (batchAction == null)
				throw new ArgumentNullException("batchAction");

			_batchSource = batchSource;
			_batchAction = obj => batchAction(this, (ItemT)obj);
			return this;
		}

		public void Prepare(PersistenceStore store)
		{
			// ----------------------------------------
			// Prepare persistence commands

			PersistenceAction mainAction = store.NewPersistenceAction();
			this.PersistenceActions = new List<PersistenceAction>();
			
			foreach (Subquery subquery in this.Subqueries)
			{
				if (!subquery.IsPrepared)
					subquery.Prepare();

				if (subquery.Template.IsStandalone && !subquery.Template.IsRoot)
				{
					// Standalone subquery, use its prepared action as-is
					this.PersistenceActions.Add(subquery.PersistenceAction);
				}
				else
				{
					// Chain the subquery action
					mainAction.Append(subquery.PersistenceAction);
					subquery.PersistenceAction = mainAction;

					subquery.ResultSetIndex = _resultSetCount;
					_resultSetCount++;
				}

				// Add parameters to the command object
				foreach (PersistenceParameter param in subquery.PersistenceParameters.Values)
				{
					subquery.PersistenceAction.ApplyParameter(param);
				}
			}

			// ----------------------------------------
			// Finalize the main command object
			this.PersistenceActions.Insert(0, mainAction);

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

		public IEnumerable<T> Execute()
		{
			if (this.Connection == null)
			{
				try { Connect(); }
				catch (ArgumentNullException) { throw new QueryExecutionException("There is no active thread connection - Connect must be called with a valid connection object before Execute."); }
			}

			if (!this.IsPrepared)
				this.Prepare(this.Connection.Store);

			List<T> buffer = null;

			foreach (PersistenceAction action in this.PersistenceActions)
			{
				action.Connection = this.Connection;

				// Find associated subqueries and order them by result set index
				Subquery[] subqueries = this.Subqueries.Where(s => s.PersistenceAction == action).OrderBy(s => s.ResultSetIndex).ToArray();

				IEnumerator batchEnumerator = null;
				if (_batchSource != null)
					batchEnumerator = _batchSource.GetEnumerator();

				while (batchEnumerator == null || batchEnumerator.MoveNext())
				{
					if (batchEnumerator != null)
						_batchAction(batchEnumerator.Current);

					// Add parameters to the action from all subqueries
					foreach (Subquery subquery in subqueries)
					{
						foreach (var before in subquery.Template.DelegatesBefore)
							before(subquery);

						foreach (PersistenceParameter param in subquery.PersistenceParameters.Values)
							action.ApplyParameter(param);
					}

					// Execute the command
					int resultSetIndex = 0;
					using (PersistenceAdapter adapter = action.Execute())
					{
						// TODO: Iterate result set of each persistence action
						do
						{
							Subquery subquery = subqueries[resultSetIndex];

							MappingContext context = subquery.Mapping.CreateContext(adapter, subquery);

							var results = subquery.Mapping.ApplyAndReturn(context);

							if (subquery.Template.IsRoot)
							{
								// Yield results with no buffering only if this is the root subquery and it is the last to be executed
								if (resultSetIndex == subqueries.Length - 1 && action == this.PersistenceActions[this.PersistenceActions.Count - 1])
								{
									foreach (T result in results)
										yield return result;
								}
								else
								{
									buffer = ((IEnumerable<T>)results).ToList();
								}
							}
							else
							{
								// This is required in order for the IEnumerable to execute
								foreach (object result in results) ;
							}
							resultSetIndex++;
						}
						while (adapter.NextResultSet());
					}

					// Wrap up
					foreach (Subquery subquery in subqueries)
					{
						foreach (var after in subquery.Template.DelegatesAfter)
							after(subquery);
					}

					// If we are not iterating a batch, just exit
					if (batchEnumerator == null)
						break;
				}
			}

			// If the results were buffered because of subqueries, yield the results now
			if (buffer != null)
				foreach (T result in buffer)
					yield return result;
		}
	}
}
