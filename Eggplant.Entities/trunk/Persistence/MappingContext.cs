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
		//public MappingDirection Direction { get; internal set; }

		//public Subquery ActivateSubquery { get; internal set; }
		public PersistenceChannel Stream { get; private set; }
		public IMapping ActiveMapping { get; internal set; }
		public MappingContext ParentContext { get; private set; }
		public object Target { get; internal set; }
		public EntitySpace EntitySpace { get; private set; }

		internal bool DoBreak { get; private set; }

		Dictionary<string, object> _vars;

		internal MappingContext(Query query, EntitySpace space, PersistenceChannel stream,  MappingContext baseContext = null)
		{
			this.Stream = stream;
			this.EntitySpace = space;
			this.Query = query;
			this.DoBreak = false;

			// Inherit vars from base
			_vars = baseContext != null ? new Dictionary<string, object>(baseContext._vars) : new Dictionary<string, object>();
		}

		public object GetField(string field)
		{
			return this.Stream.GetField(field);
		}

		public V GetField<V>(string field, Func<object, V> convert = null)
		{
			if (convert == null)
				return (V)this.Stream.GetField(field);
			else
				return convert(this.Stream.GetField(field));
		}

		public void SetField(string field, object value)
		{
			this.Stream.SetField(field, value);
		}

		public void SetVariable(string variable, object value)
		{
			_vars[variable] = value;
		}

		public object GetVariable(string variable)
		{
			return _vars[variable];
		}

		public V GetVariable<V>(string variable, Func<object, V> convert = null)
		{
			if (convert == null)
				return (V)_vars[variable];
			else
				return convert(_vars[variable]);
		}

		internal virtual void SetTarget(object target)
		{
			this.Target = target;
		}

		/// <summary>
		/// Stops the mapping operation and returns to the parent
		/// </summary>
		public void Break()
		{
			this.DoBreak = true;
		}
	}

	public class MappingContext<T> : MappingContext
	{
		public new T Target { get { return (T)base.Target; } }

		internal MappingContext(Query query, EntitySpace space, PersistenceChannel stream):base(query, space, stream)
		{
		}

		internal MappingContext(MappingContext baseContext): base(baseContext.Query, baseContext.EntitySpace, baseContext.Stream, baseContext)
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
