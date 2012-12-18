using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eggplant.Entities.Model;

namespace Eggplant.Entities.Persistence
{
	public class InlineMapping<T>: Mapping<T>, IInlineMapping
	{
		public Func<MappingContext<T>, bool> GroupingFunction { get; set; }

		internal InlineMapping(IMapping parent, EntitySpace space = null): base(parent, space)
		{
		}

		bool IInlineMapping.InGroup(MappingContext context)
		{
			return this.GroupingFunction((MappingContext<T>)context);
		}

		public InlineMapping<T> GroupBy(params string[] fields)
		{
			return GroupBy(context => fields.All(field => Object.Equals(context.GetField(field), context.GetVariable("__inline__" + field))));
		}

		public InlineMapping<T> GroupBy(Func<MappingContext<T>, bool> groupingFunction)
		{
			this.GroupingFunction = groupingFunction;
			return this;
		}

		#region Sugar
		// =========================

		public new InlineMapping<T> Inherit(IMapping baseMapping)
		{
			return (InlineMapping<T>)base.Inherit(baseMapping);
		}
		public new InlineMapping<T> Set(Func<MappingContext<T>, T> function)
		{
			return (InlineMapping<T>)base.Set(function);
		}
		public new InlineMapping<T> Do(Action<MappingContext<T>> action)
		{
			return (InlineMapping<T>)base.Do(action);
		}
		public new InlineMapping<T> Map<V>(string variable, Action<VariableMapping<V>> init)
		{
			return (InlineMapping<T>)base.Map<V>(variable, init);
		}
		public new InlineMapping<T> Map<V>(string variable, string field)
		{
			return (InlineMapping<T>)base.Map<V>(variable, field);
		}
		public new InlineMapping<T> Map<V>(IEntityProperty<V> property, Action<PropertyMapping<V>> init)
		{
			return (InlineMapping<T>)base.Map<V>(property, init);
		}
		public new InlineMapping<T> Map<V>(IEntityProperty<V> property, string field)
		{
			return (InlineMapping<T>)base.Map<V>(property, field);
		}
		public new InlineMapping<T> Subquery<V>(string subqueryName, Action<SubqueryMapping<V>> init)
		{
			return (InlineMapping<T>)base.Subquery<V>(subqueryName, init);
		}
		public new InlineMapping<T> Subquery(string subqueryName, Action<SubqueryMapping<object>> init)
		{
			return (InlineMapping<T>)base.Subquery(subqueryName, init);
		}
		public new InlineMapping<T> Inline<V>(Action<InlineMapping<V>> init)
		{
			return (InlineMapping<T>)base.Inline<V>(init);
		}
		public new InlineMapping<T> Inline(Action<InlineMapping<object>> init)
		{
			return (InlineMapping<T>)base.Inline(init);
		}

		// =========================
		#endregion

	}
}
