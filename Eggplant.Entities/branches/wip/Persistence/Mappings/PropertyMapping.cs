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

		internal PropertyMapping(IMapping parentMapping, EntitySpace space = null): base(parentMapping, space)
		{
		}

		IEntityProperty IPropertyMapping.Property
		{
			get { return this.Property; }
		}

		public override string ToString()
		{
			return String.Format("property \"{0}\" ({1})", this.Property.Name, typeof(T).Name);
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
		public new PropertyMapping<T> Subquery<V>(string subqueryName, Action<SubqueryMapping<V>> init)
		{
			return (PropertyMapping<T>)base.Subquery<V>(subqueryName, init);
		}
		public new PropertyMapping<T> Subquery(string subqueryName, Action<SubqueryMapping<object>> init)
		{
			return (PropertyMapping<T>)base.Subquery(subqueryName, init);
		}


		// =========================
		#endregion
	}
}
