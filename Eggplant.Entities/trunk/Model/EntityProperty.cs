using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using Eggplant.Entities.Persistence;

namespace Eggplant.Entities.Model
{

	#region Interfaces
	// ==============================

	public interface IEntityProperty
	{
		string Name { get; }
		AccessMode AccessMode { get; set; }
		bool AllowEmpty { get; set; }
		MemberInfo TargetMember { get; set; }

		IInboundMappingContext CreateInboundContext(IInboundMapping mapping, PersistenceConnection connection);
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
		public MemberInfo TargetMember { get; set; }

		public Func<EntityT, ValueT, AssignmentResult> OnAdd;
		public Func<EntityT, ValueT, AssignmentResult> OnRemove;

		public EntityProperty(string name)
		{
			this.Name = name;
		}

		//protected virtual InboundMappingContext<ValueT> CreateInboundContext(InboundMapping<ValueT> mapping, PersistenceConnection connection)
		//{
		//    return new InboundMappingContext<ValueT>(mapping, connection);
		//}

		#region Interfaces

		public virtual IInboundMappingContext CreateInboundContext(IInboundMapping mapping, PersistenceConnection connection)
		{
			return new InboundMappingContext<ValueT>((InboundMapping<ValueT>)mapping, connection);
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

		public override IInboundMappingContext CreateInboundContext(IInboundMapping mapping, PersistenceConnection connection)
		{
			return new InboundCollectionMappingContext<EntityT, ValueT>((InboundMapping<ICollection<ValueT>>)mapping, connection)
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

		public override IInboundMappingContext CreateInboundContext(IInboundMapping mapping, PersistenceConnection connection)
		{
			//return new InboundCollectionMappingContext<EntityT, KeyT>(this.Key, (InboundMapping<EntityT>)mapping, connection);
			//return new InboundCollectionMappingContext<EntityT, ValueT>(this.Value, (InboundMapping<EntityT>)mapping, connection);

			return new InboundDictionaryMappingContext<EntityT, KeyT, ValueT>((InboundMapping<IDictionary<KeyT, ValueT>>)mapping, connection)
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
