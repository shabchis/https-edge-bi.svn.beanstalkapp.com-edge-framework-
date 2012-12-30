using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Text;
using Eggplant.Entities.Cache;
using Eggplant.Entities.Model;
using Eggplant.Entities.Queries;

namespace Eggplant.Entities.Persistence
{
	public abstract class MappingContext
	{
		public Query Query { get; internal set; }
		//public MappingDirection Direction { get; internal set; }

		//public Subquery ActivateSubquery { get; internal set; }
		public PersistenceChannel IO { get; private set; }
		public IMapping ActiveMapping { get; internal set; }
		public MappingContext ParentContext { get; private set; }
		public object Target { get; internal set; }
		public EntitySpace EntitySpace { get; private set; }
		public EntityCacheManager Cache { get; private set; }

		internal bool DoBreak { get; private set; }

		Dictionary<string, object> _vars;

		internal MappingContext(Query query, EntitySpace space, PersistenceChannel io, EntityCacheManager cache, MappingContext baseContext = null)
		{
			this.IO = io;
			this.EntitySpace = space;
			this.Query = query;
			this.Cache = cache;
			this.DoBreak = false;

			// Inherit vars from base
			_vars = baseContext != null ? new Dictionary<string, object>(baseContext._vars) : new Dictionary<string, object>();
		}

		public object GetField(string field)
		{
			return this.IO.GetField(field);
		}

		public V GetField<V>(string field, Func<object, V> convert = null)
		{
			if (convert == null)
				return (V)this.IO.GetField(field);
			else
				return convert(this.IO.GetField(field));
		}

		public void SetField(string field, object value)
		{
			this.IO.SetField(field, value);
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

		public MappingContext(Query query, EntitySpace space, PersistenceChannel io, EntityCacheManager cache):base(query, space, io, cache)
		{
		}

		internal MappingContext(MappingContext baseContext): base(baseContext.Query, baseContext.EntitySpace, baseContext.IO, baseContext.Cache, baseContext)
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
