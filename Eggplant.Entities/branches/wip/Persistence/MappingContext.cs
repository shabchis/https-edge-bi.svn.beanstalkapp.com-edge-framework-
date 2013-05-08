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
		public PersistenceAdapter Adapter { get; private set; }
		public IMapping CurrentMapping { get; internal set; }
		public Subquery CurrentSubquery { get; internal set; }
		public MappingContext ParentContext { get; private set; }
		public MappingDirection Direction { get; private set; }
		
		public EntityCache Cache { get { return this.Adapter.Connection.Cache; } }
		public object MappedValue { get { return GetMappedValue(); } set { SetMappedValue(value); } }
		
		public Type MappedValueType { get; set; }

		internal bool DoBreak { get; private set; }

		object _mappedValue;
		internal bool IsMappedValueSet { get; private set; }
		Dictionary<string, object> _vars;

		internal MappingContext(PersistenceAdapter adapter, Subquery subquery, IMapping mapping, MappingDirection direction, MappingContext parentContext = null)
		{
			this.Adapter = adapter;
			this.Direction = direction;
			this.ParentContext = parentContext;
			this.CurrentSubquery = subquery;
			this.CurrentMapping = mapping;

			this.Reset();
		}

		/// <summary>
		/// Resets the context for iterative operations
		/// </summary>
		internal void Reset()
		{
			_mappedValue = null;
			this.MappedValueType = null;
			this.IsMappedValueSet = false;
			this.DoBreak = false;

			// Inherit vars from base, if present
			_vars = this.ParentContext != null && this.ParentContext._vars != null ?
				new Dictionary<string, object>(this.ParentContext._vars) :
				null;
		}

		private void ValidateState()
		{
			if (this.Direction != MappingDirection.Inbound && this.Direction != MappingDirection.Outbound)
				throw new InvalidOperationException("MappingContext.Direction must be either inbound or outbound. It is set to something unrecognized.");
		}

		public bool HasField(string field)
		{
			ValidateState();

			return this.Direction == MappingDirection.Inbound ?
				this.Adapter.HasInboundField(field) :
				this.Adapter.HasOutboundField(field);
		}

		public object GetField(string field)
		{
			return this.GetField<object>(field);
		}

		public V GetField<V>(string field, Func<object, V> convert = null)
		{
			ValidateState();

			object rawVal = this.Direction == MappingDirection.Inbound?
				this.Adapter.GetInboundField(field) :
				this.Adapter.GetOutboundField(field)
			;

			V convertedVal;
			if (convert == null)
				convertedVal = (V)rawVal;
			else
				convertedVal = convert(rawVal);

			return convertedVal;
		}
		
		public void SetField(string field, object value)
		{
			this.SetField<object>(field, value);
		}
		public void SetField<V>(string field, V value, Func<V, object> convert = null)
		{
			ValidateState();

			object convertedValue = convert == null ? (object)value : convert(value);

			if (this.Direction == MappingDirection.Inbound)
				throw new InvalidOperationException("Setting an inbound field is not a valid operation. It doesn't make sense anyway.");
			else
				this.Adapter.SetOutboundField(field, convertedValue);
		}

		public void SetVariable(string variable, object value)
		{
			if (_vars == null)
				_vars = new Dictionary<string, object>();

			_vars[variable] = value;
		}

		public object GetVariable(string variable)
		{
			if (_vars == null)
				return null;

			return _vars[variable];
		}

		public V GetVariable<V>(string variable, Func<object, V> convert = null)
		{
			object val = _vars == null ? null : _vars[variable];
			if (convert == null)
				return (V)val;
			else
				return convert(val);
		}

		protected virtual object GetMappedValue()
		{
			return _mappedValue;
		}

		protected virtual void SetMappedValue(object mappedValue)
		{
			_mappedValue = mappedValue;
			this.IsMappedValueSet = true;
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
		public new T MappedValue { get { return (T)base.MappedValue; } set { base.MappedValue = value; } }

		internal MappingContext(PersistenceAdapter adapter, Subquery subquery, Mapping<T> mapping, MappingDirection direction) :
			base(adapter, subquery, mapping, direction, null)
		{
		}

		internal MappingContext(MappingContext parentContext, Mapping<T> mapping):
			base(parentContext.Adapter, parentContext.CurrentSubquery, mapping, parentContext.Direction, parentContext)
		{
		}

		protected override void SetMappedValue(object mappedValue)
		{
			if (!(mappedValue is T) && !(typeof(T).IsClass && mappedValue == null ))
				throw new MappingException(String.Format("The mapping context expects a value of type {0}.", typeof(T).FullName));

			base.SetMappedValue(mappedValue);
		}
	}
}
