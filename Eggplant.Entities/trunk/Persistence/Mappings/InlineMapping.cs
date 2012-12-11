using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eggplant.Entities.Model;

namespace Eggplant.Entities.Persistence
{
	public class InlineMapping<T>: Mapping<T>, IInlineMapping
	{
		public Func<MappingContext<T>, bool> MatchFunction { get; set; }

		internal InlineMapping(EntitySpace space): base(space)
		{
		}

		bool IInlineMapping.IsMatch(MappingContext context)
		{
			return this.MatchFunction((MappingContext<T>)context);
		}

		public InlineMapping<T> Match(params string[] fields)
		{
			return Match(context => fields.All(field => Object.Equals(context.GetField(field), context.GetVariable("__inline__" + field))));
		}

		public InlineMapping<T> Match(Func<MappingContext<T>, bool> matchFunction)
		{
			this.MatchFunction = matchFunction;
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
		public new InlineMapping<T> MapSubquery<V>(string subqueryName, Action<SubqueryMapping<V>> init)
		{
			return (InlineMapping<T>)base.MapSubquery<V>(subqueryName, init);
		}
		public new InlineMapping<T> MapSubquery(string subqueryName, Action<SubqueryMapping<object>> init)
		{
			return (InlineMapping<T>)base.MapSubquery(subqueryName, init);
		}

		// =========================
		#endregion

	}
}
