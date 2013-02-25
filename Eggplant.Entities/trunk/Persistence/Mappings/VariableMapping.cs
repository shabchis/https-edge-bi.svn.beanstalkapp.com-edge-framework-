using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eggplant.Entities.Model;

namespace Eggplant.Entities.Persistence
{
	public class VariableMapping<T> : Mapping<T>, IVariableMapping
	{
		public string Variable { get; internal set; }

		internal VariableMapping(IMapping parentMapping, EntitySpace space = null)
			: base(parentMapping, space)
		{
		}

		public override void Apply(MappingContext<T> context)
		{
			base.Apply(context);
		}

		public override string ToString()
		{
			return String.Format("variable \"{0}\" ({1})", this.Variable, typeof(T).Name);
		}


		#region Sugar
		// =========================
		public new VariableMapping<T> Inherit(IMapping baseMapping)
		{
			return (VariableMapping<T>)base.Inherit(baseMapping);
		}
		public new VariableMapping<T> Set(Func<MappingContext<T>, T> function)
		{
			return (VariableMapping<T>)base.Set(function);
		}
		public new VariableMapping<T> Do(Action<MappingContext<T>> action)
		{
			return (VariableMapping<T>)base.Do(action);
		}
		public new VariableMapping<T> Map<V>(string variable, Action<VariableMapping<V>> init)
		{
			return (VariableMapping<T>)base.Map<V>(variable, init);
		}
		public new VariableMapping<T> Map<V>(string variable, string field)
		{
			return (VariableMapping<T>)base.Map<V>(variable, field);
		}
		public new VariableMapping<T> Map<V>(IEntityProperty<V> property, Action<PropertyMapping<V>> init)
		{
			return (VariableMapping<T>)base.Map<V>(property, init);
		}
		public new VariableMapping<T> Map<V>(IEntityProperty<V> property, string field)
		{
			return (VariableMapping<T>)base.Map<V>(property, field);
		}
		public new VariableMapping<T> Subquery<V>(string subqueryName, Action<SubqueryMapping<V>> init)
		{
			return (VariableMapping<T>)base.Subquery<V>(subqueryName, init);
		}
		public new VariableMapping<T> Subquery(string subqueryName, Action<SubqueryMapping<object>> init)
		{
			return (VariableMapping<T>)base.Subquery(subqueryName, init);
		}
		public new VariableMapping<T> Inline<V>(Action<InlineMapping<V>> init)
		{
			return (VariableMapping<T>)base.Inline<V>(init);
		}
		public new VariableMapping<T> Inline(Action<InlineMapping<object>> init)
		{
			return (VariableMapping<T>)base.Inline(init);
		}

		// =========================
		#endregion
	}
}
