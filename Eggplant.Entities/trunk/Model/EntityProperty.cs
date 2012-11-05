using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using Eggplant.Entities.Persistence;
using Eggplant.Entities.Queries;

namespace Eggplant.Entities.Model
{

	#region Interfaces
	// ==============================

	public interface IEntityProperty
	{
		string Name { get; }
		AccessMode AccessMode { get; set; }
		bool AllowEmpty { get; set; }
		Type PropertyType { get; }

		object GetValue(object target);
		void SetValue(object target, object value);

		IMappingContext CreateContext(QueryBase query, IMapping mapping, MappingDirection direction);
	}

	public interface ICollectionProperty:IEntityProperty
	{
		IEntityProperty Value { get; }
	}

	public interface IDictionaryProperty : ICollectionProperty
	{
		IEntityProperty Key { get; }
	}

	public interface IEntityProperty<ValueT> : IEntityProperty
	{
		new ValueT GetValue(object target);
		new void SetValue(object target, ValueT value); 
	}

	public interface ICollectionProperty<ValueT> : IEntityProperty<ValueT>, ICollectionProperty
	{
		new IEntityProperty<ValueT> Value { get; }
	}

	public interface IDictionaryProperty<KeyT, ValueT> : ICollectionProperty<ValueT>, IDictionaryProperty
	{
		new IEntityProperty<KeyT> Key { get; }
	}

	// ==============================
	#endregion

	public abstract class EntityProperty<EntityT, ValueT> : IEntityProperty<ValueT>
	{
		public string Name { get; private set; }
		public AccessMode AccessMode { get; set; }
		public bool AllowEmpty { get; set; }

		public Func<EntityT, ValueT> Getter { get; set; }
		public Action<EntityT, ValueT> Setter { get; set; }

		//public Func<EntityT, ValueT, AssignmentResult> OnAdd;
		//public Func<EntityT, ValueT, AssignmentResult> OnRemove;

		public EntityProperty(string name)
		{
			this.Name = name;
		}

		public ValueT GetValue(EntityT target)
		{
			return this.Getter(target);
		}

		public void SetValue(EntityT target, ValueT value)
		{
			this.Setter(target, value);
		}

		public virtual IMappingContext CreateContext(QueryBase query, IMapping mapping, MappingDirection dir)
		{
			return new MappingContext<ValueT>(query, (Mapping<ValueT>)mapping, dir);
		}

		public Type PropertyType { get { return typeof(ValueT); } }

		
		#region IEntityProperty<ValueT> Members

		ValueT IEntityProperty<ValueT>.GetValue(object target)
		{
			return this.GetValue((EntityT)target);
		}

		void IEntityProperty<ValueT>.SetValue(object target, ValueT value)
		{
			this.SetValue((EntityT)target, value);
		}

		#endregion

		#region IEntityProperty Members

		MethodInfo IEntityProperty.Getter
		{
			get { return this.Getter.Method; }
		}

		MethodInfo IEntityProperty.Setter
		{
			get { return this.Setter.Method; }
		}


		object IEntityProperty.GetValue(object target)
		{
			return this.GetValue((EntityT)target);
		}

		void IEntityProperty.SetValue(object target, object value)
		{
			this.SetValue((EntityT)target, (ValueT) value);
		}

		#endregion
	}

	public abstract class ScalarProperty<EntityT, ValueT> : EntityProperty<EntityT, ValueT>
	{
		public ScalarProperty(string name):base(name)
		{
		}
	}

	public class ValueProperty<EntityT, ValueT> : ScalarProperty<EntityT, ValueT>
	{
		public ValueT DefaultValue;
		public ValueT EmptyValue;

		public ValueProperty(string name) : base(name)
		{
		}
	}

	public class ReferenceProperty<EntityT, ValueT> : ScalarProperty<EntityT, ValueT>
	{
		public ReferenceProperty(string name) : base(name)
		{
		}
	}

	public class CollectionProperty<EntityT, ValueT> : EntityProperty<EntityT, ICollection<ValueT>>, ICollectionProperty<ValueT>
	{
		public ScalarProperty<EntityT, ValueT> Value;
		
		public CollectionProperty(string name) : base(name)
		{
		}

		public override IMappingContext CreateContext(QueryBase query, IMapping mapping, MappingDirection dir)
		{
			return new CollectionMappingContext<EntityT, ValueT>(query, (Mapping<ICollection<ValueT>>)mapping, dir)
			{
				ValueProperty = this.Value
			};
		}

		#region Interfaces

		IEntityProperty ICollectionProperty.Value
		{
			get { return this.Value; }
		}
		
		IEntityProperty<ValueT> ICollectionProperty<ValueT>.Value
		{
			get { return this.Value; }
		}


		#endregion
	}

	public class DictionaryProperty<EntityT, KeyT, ValueT> : EntityProperty<EntityT, IDictionary<KeyT, ValueT>>, IDictionaryProperty<KeyT, ValueT>
	{
		public ScalarProperty<EntityT, ValueT> Value;
		public ScalarProperty<EntityT, KeyT> Key;

		public DictionaryProperty(string name) : base(name)
		{
		}

		public override IMappingContext CreateContext(QueryBase query, IMapping mapping, MappingDirection dir)
		{
			//return new InboundCollectionMappingContext<EntityT, KeyT>(this.Key, (InboundMapping<EntityT>)mapping, connection);
			//return new InboundCollectionMappingContext<EntityT, ValueT>(this.Value, (InboundMapping<EntityT>)mapping, connection);

			return new DictionaryMappingContext<EntityT, KeyT, ValueT>(query, (Mapping<IDictionary<KeyT, ValueT>>)mapping, dir)
			{
				KeyProperty = this.Key,
				ValueProperty = this.Value
			};
		}

		#region Interfaces

		IEntityProperty IDictionaryProperty.Key
		{
			get { return this.Key; }
		}
		
		IEntityProperty<KeyT> IDictionaryProperty<KeyT, ValueT>.Key
		{
			get { return this.Key; }
		}

		IEntityProperty ICollectionProperty.Value
		{
			get { return this.Value; }
		}

		IEntityProperty<ValueT> ICollectionProperty<ValueT>.Value
		{
			get { return this.Value; }
		}

		#endregion
	}


	public enum AccessMode
	{
		ReadOnly,
		WriteAlways,
		WriteWhenDetached
	}

	public enum AssignmentResultType
	{
		Allow,
		Restrict,
		Delete
	}

	public struct AssignmentResult
	{
		public AssignmentResultType ResultType;
		public int ResultCode;
		public string Message;

		public AssignmentResult(AssignmentResultType resultType, int resultCode, string message)
		{
			this.ResultType = resultType;
			this.ResultCode = resultCode;
			this.Message = message;
		}

		public static AssignmentResult Restrict(string message = null, int resultCode = 0)
		{
			return new AssignmentResult(AssignmentResultType.Restrict, resultCode, message);
		}
		public static AssignmentResult Allow(string message = null, int resultCode = 0)
		{
			return new AssignmentResult(AssignmentResultType.Allow, resultCode, message);
		}
		public static AssignmentResult Delete(string message = null, int resultCode = 0)
		{
			return new AssignmentResult(AssignmentResultType.Delete, resultCode, message);
		}
	}
}
