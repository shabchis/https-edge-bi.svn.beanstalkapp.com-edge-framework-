using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Data;
using System.ComponentModel;
using System.Collections;

namespace Eggplant.Persistence
{
	/// <summary>
	/// 
	/// </summary>
	public class EntityEventArgs: EventArgs
	{
		public IEntityProperty Property;
		public Entity Entity;

		public EntityEventArgs(Entity entity, IEntityProperty property)
		{
			Entity = entity;
			Property = property;
		}
	}

	/// <summary>
	/// 
	/// </summary>
	public class ValueTranslationEventArgs: EntityEventArgs
	{
		public object Input;
		public object Output;
		public bool Cancel = false;

		public ValueTranslationEventArgs(Entity entity, IEntityProperty property): base(entity, property)
		{
		}

	}

	/// <summary>
	/// 
	/// </summary>
	public class ValidationEventArgs: EntityEventArgs
	{
		public bool Valid;
		public string ValidationMessage;
		public object Value;

		public ValidationEventArgs(Entity entity, IEntityProperty property)
			: base(entity, property)
		{
		}

	}

	/// <summary>
	/// 
	/// </summary>
	public class ValueChangedEventArgs: EntityEventArgs
	{
		public object ValueBefore;
		public object ValueAfter;

		public ValueChangedEventArgs(Entity entity, IEntityProperty property, object valueBefore, object valueAfter): base(entity, property)
		{
			ValueBefore = valueBefore;
			ValueAfter = valueAfter;
		}
	}

	/// <summary>
	/// 
	/// </summary>
	public class DependencyStateChangedEventArgs: EntityEventArgs
	{
		public Dependency Dependency;
		public bool IsAvailable;

		public DependencyStateChangedEventArgs(Entity entity, IEntityProperty property, Dependency dependency, bool available)
			: base(entity, property)
		{
			Dependency = dependency;
			IsAvailable = available;
		}
	}

	//public class ReferenceEntityRequiredEventArgs<ReferenceIdentityT>: EntityEventArgs
	//{
	//    public ReferenceIdentityT ReferenceID;
	//    public Entity ReferenceEntity;
	//}
}
