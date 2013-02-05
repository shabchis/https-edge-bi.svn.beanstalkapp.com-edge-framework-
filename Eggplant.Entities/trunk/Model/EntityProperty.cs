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
		void SetValue(object target, ValueT value); 
	}

	// ==============================
	#endregion

	public class EntityProperty<EntityT, ValueT> : IEntityProperty<ValueT>
	{
		public string Name { get; private set; }
		public AccessMode AccessMode { get; set; }
		public bool AllowEmpty { get; set; }
		public Func<EntityT, ValueT> Getter { get; set; }
		public Action<EntityT, ValueT> Setter { get; set; }


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
			get
			{
				return this.Getter;
			}
			set
			{
				Func<EntityT, ValueT> getter;
				if (value is Func<EntityT, ValueT>)
					// Use the func as-is
					getter = (Func<EntityT, ValueT>)value;
				else if (value is Func<object, object>)
					// Nest the func inside a strong-typed func
					getter = new Func<EntityT, ValueT>(entity => (ValueT)((Func<object, object>)value)(entity));
				else
					throw new ArgumentException(String.Format("The getter for this property must be either a Func<{0}, {1}> or a Func<object, object>.", typeof(EntityT).Name, typeof(ValueT).Name));

				this.Getter = getter;
			}
		}

		Delegate IEntityProperty.Setter
		{
			get
			{
				return this.Setter;
			}
			set
			{
				Action<EntityT, ValueT> setter;
				if (value is Action<EntityT, ValueT>)
					// Use the action as-is
					setter = (Action<EntityT, ValueT>)value;
				else if (value is Action<object, object>)
					// Nest the action inside a strong-typed action
					setter = new Action<EntityT, ValueT>((entity, val) => ((Action<object, object>)value)(entity, val));
				else
					throw new ArgumentException(String.Format("The setter for this property must be either a Action<{0}, {1}> or a Action<object, object>.", typeof(EntityT).Name, typeof(ValueT).Name));

				this.Setter = setter;
			}
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
