﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eggplant.Entities.Model;

namespace Eggplant.Entities.Persistence
{
	public class SubqueryMapping<T>:Mapping<T>, ISubqueryMapping
	{
		public string SubqueryName { get; set; }

		internal SubqueryMapping(EntitySpace space): base(space)
		{
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
		public new SubqueryMapping<T> MapSubquery<V>(string subqueryName, Action<SubqueryMapping<V>> init)
		{
			return (SubqueryMapping<T>)base.MapSubquery<V>(subqueryName, init);
		}
		public new SubqueryMapping<T> MapSubquery(string subqueryName, Action<SubqueryMapping<T>> init)
		{
			return (SubqueryMapping<T>)base.MapSubquery(subqueryName, init);
		}
		public new SubqueryMapping<T> MapInline<V>(Action<InlineMapping<V>> init)
		{
			return (SubqueryMapping<T>)base.MapInline<V>(init);
		}
		public new SubqueryMapping<T> MapInline(Action<InlineMapping<object>> init)
		{
			return (SubqueryMapping<T>)base.MapInline(init);
		}

		// =========================
		#endregion
	}
}
