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

		Delegate Getter { get; set; }
		Delegate Setter { get; set; }

		object GetValue(object target);
		void SetValue(object target, object value);
	}

	public interface IEntityProperty<ValueT> : IEntityProperty
	{
		new ValueT GetValue(object target);
		new void SetValue(object target, ValueT value); 
	}

	// ==============================
	#endregion

	public abstract class EntityProperty<EntityT, ValueT> : IEntityProperty<ValueT>
	{
		public string Name { get; private set; }
		public AccessMode AccessMode { get; set; }
		public bool AllowEmpty { get; set; }

		public Func<EntityT, ValueT> Getter
		{
			get
			{
				if (this.Getter == null)
					return null;

				if (!(((IEntityProperty)this).Getter is Func<EntityT, ValueT>))
					throw new InvalidCastException(String.Format("The getter for this property is not a Func<{0}, {1}>; cast the property to IEntityProperty in order to retrieve the delegate.", typeof(EntityT).Name, typeof(ValueT).Name));

				return (Func<EntityT, ValueT>) ((IEntityProperty)this).Getter;
			}

			set
			{
				((IEntityProperty)this).Getter = value; 
			}
		}
		public Action<EntityT, ValueT> Setter
		{
			get
			{
				if (this.Getter == null)
					return null;

				if (!(((IEntityProperty)this).Setter is Action<EntityT, ValueT>))
					throw new InvalidCastException(String.Format("The setter for this property is not a Action<{0}, {1}>; cast the property to IEntityProperty in order to retrieve the delegate.", typeof(EntityT).Name, typeof(ValueT).Name));

				return (Action<EntityT, ValueT>)((IEntityProperty)this).Setter;
			}

			set
			{
				((IEntityProperty)this).Setter = value;
			}
		}

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

		Delegate IEntityProperty.Getter
		{
			get;
			set;
		}

		Delegate IEntityProperty.Setter
		{
			get;
			set;
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
