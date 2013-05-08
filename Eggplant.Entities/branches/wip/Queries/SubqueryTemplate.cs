using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using Eggplant.Entities.Model;
using Eggplant.Entities.Persistence;
using System.Text.RegularExpressions;
using System.IO;

namespace Eggplant.Entities.Queries
{
	public class SubqueryTemplate: QueryTemplateBase
	{
		//public bool IsBatched { get; set; }
		//public bool IsAppendable { get; set; }
		public bool IsDeferred { get; set; }
		public QueryTemplate Template { get; internal set; }
		public string Name { get; set; }
		public PersistenceAction PersistenceAction { get; set; }
		public Dictionary<string, SubqueryConditionalColumn> ConditionalColumns { get; private set; }
		internal List<Action<Subquery>> DelegatesBefore = new List<Action<Subquery>>();
		internal List<Action<Subquery>> DelegatesAfter = new List<Action<Subquery>>();

		public SubqueryTemplate(EntitySpace space): base(space)
		{
			this.ConditionalColumns = new Dictionary<string, SubqueryConditionalColumn>();
		}

		public bool IsRoot
		{
			get { return this.Template.RootSubqueryTemplate == this; }
		}

		public SubqueryTemplate Param(string paramName, object defaultValue = null, MappingDirection direction = MappingDirection.Outbound, PersistenceParameterOptions options = null)
		{
			this.PersistenceAction.Parameters[paramName] = new PersistenceParameter(paramName, defaultValue, direction, options);
			return this;
		}

		public SubqueryTemplate ParamFromInput(string paramName, string inputName,
			MappingDirection direction = MappingDirection.Outbound,
			Func<object, object> convertOut = null,
			Func<object, object> convertIn = null,
			PersistenceParameterOptions options = null)
		{
			// Define it
			this.Param(paramName, null, direction, options);

			BeforeExecute(sq =>
			{
				QueryInput p = sq.GetQueryInput(inputName);
				sq.Param(paramName, convertOut == null ? p.Value : convertOut(p.Value));
			});
						
			return this;
		}

		public SubqueryTemplate ParamsFromMappedValue(object valueToMap, IMapping mapToUse)//, PersistenceDirection direction = PersistenceDirection.Out)
		{
			this.BeforeExecute(sq =>
			{
				throw new NotImplementedException("ParamsFromMappedValue out");
			});
			
			return this;
		}

		public SubqueryTemplate ParamsFromMappedInput(string inputNameToMap, IMapping mapToUse)//, PersistenceDirection direction = PersistenceDirection.Out)
		{
			//if (direction.HasFlag(PersistenceDirection.Out))
			//{
				//this.BeforeExecute(sq =>
				//{
				//    MappingContext context = mapToUse.CreateContext(adapter, sq, MappingDirection.Outbound);
				//    context.Target = sq.Inputs[inputNameToMap].Value;
				//    mapToUse.Apply(context);
				//    foreach (var field in context.Fields)
				//        adapter.SetInboundField(field.Key, field.Value);
				//});
			//}
			//if (direction.HasFlag(PersistenceDirection.In))
			//{
			//    this.AfterExecute(sq =>
			//    {
			//        throw new NotImplementedException("ParamsFromMappedValue in");
			//    });
			//}
			return this;
		}

		public SubqueryTemplate BeforeExecute(Action<Subquery> action)
		{
			this.DelegatesBefore.Add(action);
			return this;
		}

		public SubqueryTemplate AfterExecute(Action<Subquery> action)
		{
			this.DelegatesAfter.Add(action);
			return this;
		}

		#region Condition columns - not fully implemented
		/*
		public SubqueryTemplate ConditionalColumn(string column, IEntityProperty mappedProperty)
		{
			return ConditionalColumn(column, column, mappedProperty);
		}

		public SubqueryTemplate ConditionalColumn(string columnAlias, string columnSyntax, IEntityProperty mappedProperty)
		{
			this.ConditionalColumns[columnAlias] = new SubqueryConditionalColumn()
			{
				ColumnAlias = columnAlias,
				ColumnSyntax = columnSyntax,
				Condition = subquery => subquery.SelectList.Count == 0 || subquery.SelectList.Contains(mappedProperty),
				MappedProperty = mappedProperty
			};

			return this;
		}

		public SubqueryTemplate ConditionalColumn(string column, Func<Subquery, bool> condition)
		{
			return ConditionalColumn(column, column, condition);
		}

		public SubqueryTemplate ConditionalColumn(string columnAlias, string columnSyntax, Func<Subquery, bool> condition)
		{
			this.ConditionalColumns[columnAlias] = new SubqueryConditionalColumn()
			{
				ColumnAlias = columnAlias,
				ColumnSyntax = columnSyntax,
				Condition = condition,
				MappedProperty = null
			};

			return this;
		}
		*/
		#endregion
	}

	public class SubqueryConditionalColumn
	{
		public string ColumnAlias;
		public string ColumnSyntax;
		public IEntityProperty MappedProperty;
		public Func<Subquery, bool> Condition;
	}

}
