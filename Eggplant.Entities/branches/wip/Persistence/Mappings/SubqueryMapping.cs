using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eggplant.Entities.Model;

namespace Eggplant.Entities.Persistence
{
	public class SubqueryMapping<T>: Mapping<T>, ISubqueryMapping
	{
		public string SubqueryName { get; set; }

		internal SubqueryMapping(IMapping parent, EntitySpace space = null): base(parent, space)
		{
		}

		public override string ToString()
		{
			return String.Format("subquery \"{0}\" ({1})", this.SubqueryName, typeof(T).Name);
		}

		#region Sugar
		// =========================
		public new SubqueryMapping<T> Inherit(IMapping baseMapping)
		{
			return (SubqueryMapping<T>)base.Inherit(baseMapping);
		}
		public new SubqueryMapping<T> Set(Func<MappingContext<T>, T> function)
		{
			return (SubqueryMapping<T>)base.Set(function);
		}
		public new SubqueryMapping<T> Do(Action<MappingContext<T>> action)
		{
			return (SubqueryMapping<T>)base.Do(action);
		}
		public new SubqueryMapping<T> Map<V>(string variable, Action<VariableMapping<V>> init)
		{
			return (SubqueryMapping<T>)base.Map<V>(variable, init);
		}
		public new SubqueryMapping<T> Map<V>(string variable, string field)
		{
			return (SubqueryMapping<T>)base.Map<V>(variable, field);
		}
		public new SubqueryMapping<T> Map<V>(IEntityProperty<V> property, Action<PropertyMapping<V>> init)
		{
			return (SubqueryMapping<T>)base.Map<V>(property, init);
		}
		public new SubqueryMapping<T> Map<V>(IEntityProperty<V> property, string field)
		{
			return (SubqueryMapping<T>)base.Map<V>(property, field);
		}
		public new SubqueryMapping<T> Subquery<V>(string subqueryName, Action<SubqueryMapping<V>> init)
		{
			return (SubqueryMapping<T>)base.Subquery<V>(subqueryName, init);
		}
		public new SubqueryMapping<T> Subquery(string subqueryName, Action<SubqueryMapping<object>> init)
		{
			return (SubqueryMapping<T>)base.Subquery(subqueryName, init);
		}

		// =========================
		#endregion
	}
}
