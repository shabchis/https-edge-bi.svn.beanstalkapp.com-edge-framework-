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

		IEnumerable _batchSource = null;
		Action<object> _batchAction = null;
		
		internal Query(QueryTemplate<T> template)
		{
			this.Template = template;
			this.EntitySpace = template.EntitySpace;

			foreach (QueryInput parameter in template.Inputs.Values)
				this.Inputs.Add(parameter.Name, parameter.Clone());

			// Find relevant subqueries
			var subqueryNames = new Dictionary<string, ISubqueryMapping>();
			if (template.Mapping != null)
			{
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
			}

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

		public new Query<T> Input<V>(string inputName, V value)
		{
			base.Input<V>(inputName, value);
			return this;
		}

		public Query<T> Batch<ItemT>(IEnumerable<ItemT> batchSource, string inputName)
		{
			return this.Batch<ItemT>(batchSource, (q, item) => q.Input<ItemT>(inputName, item));
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

				if (!subquery.Template.IsRoot && !subquery.PersistenceAction.IsAppendable)
				{
					// Standalone subquery, use its prepared action as-is
					this.PersistenceActions.Add(subquery.PersistenceAction);
				}
				else
				{
					// Chain the subquery action
					mainAction.Append(subquery.PersistenceAction);
					subquery.PersistenceAction = mainAction;

					subquery.InboundSetIndex = _resultSetCount;
					_resultSetCount++;
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

		class SubqueryExecutionData
		{
			public MappingContext OutboundContext;
			public MappingContext InboundContext;
			public IEnumerator OutboundEnumerator;
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

			// Buffer to hold results when processing subqueries
			List<T> buffer = null;

			// Holds adapters for each action
			var adapters = new Dictionary<PersistenceAction, PersistenceAdapter>();

			IEnumerator batchEnumerator = null;
			if (_batchSource != null)
				batchEnumerator = _batchSource.GetEnumerator();

			// Holds current input values for passing on to the adapter
			var inputValues = new Dictionary<QueryInput, object>();
			foreach (var input in this.Inputs)
				inputValues.Add(input.Value, input.Value.Value);

			while (batchEnumerator == null || batchEnumerator.MoveNext())
			{
				foreach (PersistenceAction action in this.PersistenceActions)
				{
					// Get an adapter - reuse an existing one if available
					PersistenceAdapter adapter = null;
					if (!adapters.TryGetValue(action, out adapter) || !adapter.IsReusable)
					{
						// Close the existing one since it is not reusable
						if (adapter != null)
							adapter.End();

						adapters[action] = adapter = action.GetAdapter(this.Connection);
					}

					// Find associated subqueries and order them by inbound set index
					Subquery[] subqueries = this.Subqueries.Where(s => s.PersistenceAction == action).OrderBy(s => s.InboundSetIndex).ToArray();

					//// Stuff to do before executing
					//foreach (Subquery subquery in subqueries)
					//{
					//    foreach (var before in subquery.Template.DelegatesBefore)
					//        before(subquery);
					//}

					var executionDataCache = new Dictionary<Subquery, SubqueryExecutionData>();

					// Begin processing
					adapter.Begin();

					bool done = false;
					while (!done)
					{
						adapter.NewOutboundRow();

						// Each subquery associated with this action must map its outbound fields
						foreach (Subquery subquery in subqueries)
						{
							SubqueryExecutionData executionData;
							if (!executionDataCache.TryGetValue(subquery, out executionData))
							{
								executionData.OutboundEnumerator = subquery.Mapping.GetEnumerator();
								executionData.OutboundContext = subquery.Mapping.CreateContext(adapter, subquery, MappingDirection.Outbound);
								executionDataCache.Add(subquery, executionData);
							}

							if (executionData.OutboundEnumerator != null)
							{
								// Map the fields and indicate wheter we are done sending outbound rows
								subquery.Mapping.Apply(executionData.OutboundContext);
								executionData.OutboundContext.Reset();

								// Advance the outbound enumerator, if there is nothing else to output mark remove it
								executionData.OutboundEnumerator = executionData.OutboundEnumerator.MoveNext() ? executionData.OutboundEnumerator : null;
							}

							// Not enumerator means nothing to output means we finished
							done &= executionData.OutboundEnumerator == null;
						}

						// Submit the entire row
						bool hasResults = adapter.SubmitOutboundRow();

						// Not every outbound row will result in inbound rows, but when it does, iterate them
						if (hasResults)
						{
							while (adapter.NextInboundSet())
							{
								Subquery subquery = subqueries[adapter.InboundSetIndex];
								MappingContext inboundContext = subquery.Mapping.CreateContext(adapter, subquery, MappingDirection.Inbound);

								// Process each row as it comes in
								while (adapter.NextInboundRow())
								{
									subquery.Mapping.Apply(inboundContext);
									var result = (T)inboundContext.MappedValue;

									// Inbound rows on the root query need to be transformed into results
									if (subquery.Template.IsRoot)
									{
										// Yield results with no buffering only if this is the root subquery and it is the last to be executed
										if (adapter.InboundSetIndex == subqueries.Length - 1 && action == this.PersistenceActions[this.PersistenceActions.Count - 1])
										{
											yield return result;
										}
										else
										{
											buffer.Add((T)inboundContext.MappedValue);
										}
									}
									inboundContext.Reset();
								}
							}
						}
					}

					adapter.End();

					//// Stuff to do after executing
					//foreach (Subquery subquery in subqueries)
					//{
					//    foreach (var after in subquery.Template.DelegatesAfter)
					//        after(subquery);
					//}

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
