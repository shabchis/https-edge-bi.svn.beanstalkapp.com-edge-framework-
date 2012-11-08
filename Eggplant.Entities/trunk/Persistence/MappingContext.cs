using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Text;
using Eggplant.Entities.Model;
using Eggplant.Entities.Queries;

namespace Eggplant.Entities.Persistence
{
	public class MappingContext<T> : Mapping<T>, IMappingContext
	{
		public QueryBase Query { get; private set; }
		public MappingDirection Direction { get; private set; }
		public object Target { get; internal set; }

		internal MappingContext(QueryBase query, Mapping<T> mapping, MappingDirection dir)
			: base(mapping.EntitySpace)
		{
			this.ResultSetName = mapping.ResultSetName;
			this.MappingFunction = mapping.MappingFunction;
			this.Property = mapping.Property;

			foreach (IMapping sub in mapping.SubMappings)
			{
				this.SubMappings.Add(sub.CreateContext(this.Query, dir));
			}
		}

		public V GetField<V>(string field, Func<object, V> convertFunction = null)
		{
			throw new NotImplementedException();
		}

		public MappingContext<T> SetField(string field, object value)
		{
			if (this.Direction != MappingDirection.Outbound)
				throw new InvalidOperationException("Cannot set data set fields during an inbound mapping operation.");

			throw new NotImplementedException();
			return this;
		}

		public MappingContext<T> SetValue(T value)
		{
			if (this.Direction != MappingDirection.Inbound)
				throw new InvalidOperationException("Cannot assign entity property values during an inbound mapping operation.");

			if (this.Property == null)
				throw new MappingException("Cannot assign value because the mapping property is null.");

			if (this.Target == null)
				throw new MappingException("Cannot assign value because the mapping target is null.");

			this.Property.SetValue(this.Target, value);

			return this;
		}

		// TODO: add more FromSubquery overloads till T5?

		public MappingContext<T> FromSubquery<V>(string subqueryName, Action<MappingContext<V>> function)
		{
			this.SubMappings.Add(new Mapping<V>(this.EntitySpace) { MappingFunction = function });
			return this;
		}

		public MappingContext<T> FromSubquery<T1, T2>(string subqueryName, Action<MappingContext<T1>> function1, Action<MappingContext<T2>> function2)
		{
			this.SubMappings.Add(new Mapping<T1>(this.EntitySpace) { MappingFunction = function1 });
			this.SubMappings.Add(new Mapping<T2>(this.EntitySpace) { MappingFunction = function2 });
			return this;
		}

		#region Explicit

		void IMappingContext.SetField(string field, object value)
		{
			this.SetField(field, value);
		}

		void IMappingContext.SetValue(object value)
		{
			this.SetValue((T)value);
		}

		#endregion
	}

	public enum MappingDirection
	{
		Both,
		Inbound,
		Outbound
	}
}
