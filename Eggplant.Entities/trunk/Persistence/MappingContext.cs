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
		//public Query Query { get; internal set; }
		//public MappingDirection Direction { get; internal set; }

		public PersistenceAdapter Adapter { get; private set; }
		public IMapping CurrentMapping { get; internal set; }
		public Subquery CurrentSubquery { get; internal set; }
		public MappingContext ParentContext { get; private set; }
		
		//public EntitySpace EntitySpace { get; private set; }
		public EntityCacheManager Cache { get; private set; }
		public object Target { get { return GetTarget(); } set { SetTarget(value); } }
		public Type TargetType { get; set; }

		internal bool DoBreak { get; private set; }

		object _target;
		internal bool IsTargetSet { get; private set; }
		Dictionary<string, object> _vars;

		internal MappingContext(PersistenceAdapter adapter, Subquery subquery, IMapping mapping, MappingContext parentContext = null)
		{
			this.Adapter = adapter;
			this.ParentContext = parentContext;
			this.CurrentSubquery = subquery;
			this.CurrentMapping = mapping;
			this.DoBreak = false;

			// Inherit vars from base
			_vars = parentContext != null ? new Dictionary<string, object>(parentContext._vars) : new Dictionary<string, object>();
		}

		public bool HasField(string field)
		{
			return this.Adapter.HasField(field);
		}

		public object GetField(string field)
		{
			return this.Adapter.GetField(field);
		}

		public V GetField<V>(string field, Func<object, V> convert = null)
		{
			if (convert == null)
				return (V)this.Adapter.GetField(field);
			else
				return convert(this.Adapter.GetField(field));
		}

		public void SetField(string field, object value)
		{
			this.Adapter.SetField(field, value);
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

		protected virtual object GetTarget()
		{
			//if (!IsTargetSet)
			//	throw new NotImplementedException("here we need to implicitly create a new object, or at least notify about it");
			return _target;
		}

		protected virtual void SetTarget(object target)
		{
			_target = target;
			this.IsTargetSet = true;
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
		public new T Target { get { return (T)base.Target; } set { base.Target = value; } }

		internal MappingContext(PersistenceAdapter adapter, Subquery subquery, Mapping<T> mapping):
			base(adapter, subquery, mapping, null)
		{
		}

		internal MappingContext(MappingContext parentContext, Mapping<T> mapping):
			base(parentContext.Adapter, parentContext.CurrentSubquery, mapping, parentContext)
		{
		}

		protected override void SetTarget(object target)
		{
			if (!(target is T) && !(typeof(T).IsClass && target == null ))
				throw new MappingException(String.Format("The mapping context expects a target of type {0}.", typeof(T).FullName));

			base.SetTarget(target);
		}
	}
}
