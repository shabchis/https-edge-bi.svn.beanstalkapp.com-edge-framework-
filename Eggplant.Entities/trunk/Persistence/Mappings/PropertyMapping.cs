using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eggplant.Entities.Model;

namespace Eggplant.Entities.Persistence
{
	public class PropertyMapping<T> : Mapping<T>, IPropertyMapping
	{
		public IEntityProperty<T> Property { get; internal set; }

		internal PropertyMapping(EntitySpace space) : base(space)
		{
		}

		IEntityProperty IPropertyMapping.Property
		{
			get { return this.Property; }
		}

		#region Sugar
		// =========================
		public new PropertyMapping<T> Inherit(IMapping baseMapping)
		{
			return (PropertyMapping<T>)base.Inherit(baseMapping);
		}
		public new PropertyMapping<T> Set(Func<MappingContext<T>, T> function)
		{
			return (PropertyMapping<T>)base.Set(function);
		}
		public new PropertyMapping<T> Do(Action<MappingContext<T>> action)
		{
			return (PropertyMapping<T>)base.Do(action);
		}
		public new PropertyMapping<T> Map<V>(string variable, Action<VariableMapping<V>> init)
		{
			return (PropertyMapping<T>)base.Map<V>(variable, init);
		}
		public new PropertyMapping<T> Map<V>(string variable, string field)
		{
			return (PropertyMapping<T>)base.Map<V>(variable, field);
		}
		public new PropertyMapping<T> Map<V>(IEntityProperty<V> property, Action<PropertyMapping<V>> init)
		{
			return (PropertyMapping<T>)base.Map<V>(property, init);
		}
		public new PropertyMapping<T> Map<V>(IEntityProperty<V> property, string field)
		{
			return (PropertyMapping<T>)base.Map<V>(property, field);
		}
		public new PropertyMapping<T> MapSubquery<V>(string subqueryName, Action<SubqueryMapping<V>> init)
		{
			return (PropertyMapping<T>)base.MapSubquery<V>(subqueryName, init);
		}
		public new PropertyMapping<T> MapSubquery(string subqueryName, Action<SubqueryMapping<object>> init)
		{
			return (PropertyMapping<T>)base.MapSubquery(subqueryName, init);
		}
		public new PropertyMapping<T> MapInline<V>(Action<InlineMapping<V>> init)
		{
			return (PropertyMapping<T>)base.MapInline<V>(init);
		}
		public new PropertyMapping<T> MapInline(Action<InlineMapping<object>> init)
		{
			return (PropertyMapping<T>)base.MapInline(init);
		}


		// =========================
		#endregion
	}
}
