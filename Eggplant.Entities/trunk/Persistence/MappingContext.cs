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
	public abstract class MappingContext
	{
		public Query Query { get; internal set; }
		public MappingDirection Direction { get; internal set; }

		public Subquery ActivateSubquery { get; internal set; }
		public IMapping ActiveMapping { get; internal set; }
		public object Target { get; internal set; }
		
		internal MappingContext(Query query, MappingDirection direction)
		{
			this.Query = query;
			this.Direction = direction;
		}

		public object GetField(string field)
		{
			return GetField<object>(field);
		}

		public V GetField<V>(string field, Func<object, V> convert = null)
		{
			throw new NotImplementedException();
		}

		public void SetField(string field, object value)
		{
			if (this.Direction != MappingDirection.Outbound)
				throw new InvalidOperationException("Cannot set data set fields during an inbound mapping operation.");

			throw new NotImplementedException();
		}

		public void SetVariable(string variable, object value)
		{
			throw new NotImplementedException();
		}

		public object GetVariable(string variable)
		{
			return GetVariable<object>(variable);
		}

		public V GetVariable<V>(string variable, Func<object, V> convert = null)
		{
			throw new NotImplementedException();
		}

		internal virtual void SetTarget(object target)
		{
			this.Target = target;
		}
	}

	public class MappingContext<T> : MappingContext
	{
		public new T Target { get { return (T)base.Target; } }

		internal MappingContext(Query query, MappingDirection direction):base(query, direction)
		{
		}

		internal override void SetTarget(object target)
		{
			if (!(target is T))
				throw new MappingException(String.Format("The mapping context expects a target of type {0}.", typeof(T).FullName));

			base.SetTarget(target);
		}
	}
}
