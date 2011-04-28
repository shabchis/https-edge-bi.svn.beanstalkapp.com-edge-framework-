using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Data;
using System.ComponentModel;
using System.Collections;

namespace Eggplant.Persistence
{
	public enum ValueTranslationDirection
	{
		RawToPublic,
		PublicToRaw
	}

	/// <summary>
	/// Interface for all EntityProperty instances.
	/// </summary>
	public interface IEntityProperty
	{
		#region Properties
		/*=========================*/

		/// <summary>
		/// 
		/// </summary>
		Type EntityType { get; }

		/// <summary>
		/// 
		/// </summary>
		Type ValueType { get; }

		/// <summary>
		/// 
		/// </summary>
		object DefaultValue { get; }

		/// <summary>
		/// 
		/// </summary>
		object EmptyValue { get; }

		/// <summary>
		/// 
		/// </summary>
		bool IsRequired { get; }

		/// <summary>
		/// 
		/// </summary>
		string DisplayName { get; }

		/// <summary>
		/// 
		/// </summary>
		string InlineName { get; }

		/// <summary>
		/// 
		/// </summary>
		string Description { get; }

		/// <summary>
		/// 
		/// </summary>
		DependencyTable Dependencies { get; }

		/*=========================*/
		#endregion

		#region Events
		/*=========================*/

		/// <summary>
		/// 
		/// </summary>
		event EventHandler<ValueChangedEventArgs> ValueChanged;

		/*=========================*/
		#endregion

		#region Methods
		/*=========================*/

		/// <summary>
		/// 
		/// </summary>
		/// <param name="parent"></param>
		/// <returns></returns>
		object GetValue(Entity parent);

		/// <summary>
		/// 
		/// </summary>
		/// <param name="parent"></param>
		/// <param name="value"></param>
		void SetValue(Entity parent, object value);

		/// <summary>
		/// 
		/// </summary>
		/// <param name="parent"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		bool Validate(Entity parent, object value);

		/// <summary>
		/// 
		/// </summary>
		/// <param name="parent"></param>
		/// <param name="value"></param>
		/// <param name="message"></param>
		/// <returns></returns>
		bool Validate(Entity parent, object value, out string message);

		/// <summary>
		/// 
		/// </summary>
		/// <param name="parent"></param>
		/// <returns></returns>
		bool IsAvailable(Entity parent);


		/// <summary>
		/// 
		/// </summary>
		/// <param name="parentProperty"></param>
		/// <returns></returns>
		Dependency GetParentDependency(IEntityProperty parentProperty);

		/*=========================*/
		#endregion
	}

	/// <summary>
	/// Generic class for properties that map to a scalar field.
	/// </summary>
	/// <typeparam name="EntityT"></typeparam>
	/// <typeparam name="ValueT"></typeparam>
	public class EntityProperty<EntityT, ValueT>: IEntityProperty where EntityT: Entity
	{
		#region Fields

		/*=========================*/

		string _inlineName;
		string _desc;
		string _displayName;
		bool _isRequired;
		ValueT _defaultValue;
		object _emptyValue;

		/*=========================*/
		#endregion

		#region Events
		/*=========================*/

		/// <summary>
		/// 
		/// </summary>
		public event EventHandler<ValueTranslationEventArgs> Getting;

		/// <summary>
		/// 
		/// </summary>
		public event EventHandler<ValueTranslationEventArgs> Setting;

		/// <summary>
		/// 
		/// </summary>
		public event EventHandler<ValidationEventArgs> Validating;

		/// <summary>
		/// 
		/// </summary>
		public event EventHandler<ValueChangedEventArgs> ValueChanged;

		/*=========================*/
		#endregion

		#region Constructors
		/*=========================*/

		/// <summary>
		/// 
		/// </summary>
		/// <param name="columnName"></param>
		public EntityProperty()
		{
		}

		public EntityProperty(ValueT defaultValue)
		{
			_defaultValue = defaultValue;
			_emptyValue = defaultValue;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="defaultValue">The value the property is set to by default.</param>
		/// <param name="emptyValue">The value that signifies an empty value (alternate to null).</param>
		public EntityProperty(ValueT defaultValue, object emptyValue)
		{
			_defaultValue = defaultValue;
			_emptyValue = emptyValue;
		}

		/*=========================*/
		#endregion

		#region Public Properties
		/*=========================*/

		/// <summary>
		/// 
		/// </summary>
		public Type EntityType
		{
			get
			{
				return typeof(EntityT);
			}
		}

		/// <summary>
		/// 
		/// </summary>
		public Type ValueType
		{
			get
			{
				return typeof(ValueT);
			}
		}

		/// <summary>
		/// 
		/// </summary>
		public string InlineName
		{
			get
			{
				return _inlineName;
			}
		}

		/// <summary>
		/// 
		/// </summary>
		public ValueT DefaultValue
		{
			get
			{
				return _defaultValue;
			}
		}

		/// <summary>
		/// 
		/// </summary>
		public object EmptyValue
		{
			get
			{
				return _emptyValue;
			}
		}

		public bool IsRequired
		{
			get
			{
				return _isRequired;
			}
		}

		public string DisplayName
		{
			get
			{
				return _displayName;
			}
		}

		public string Description
		{
			get
			{
				return _desc;
			}
		}

		/// <summary>
		/// 
		/// </summary>
		public DependencyTable Dependencies
		{
			get
			{
				return null;
			}
		}

		/*=========================*/

		#endregion

		#region Internal Methods
		/*=========================*/

		/// <summary>
		/// 
		/// </summary>
		/// <param name="e"></param>
		protected virtual void OnSetValue(ValueTranslationEventArgs e)
		{
			if (Setting != null)
				Setting(this, e);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="e"></param>
		protected virtual void OnGetValue(ValueTranslationEventArgs e)
		{
			if (Getting != null)
				Getting(this, e);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="e"></param>
		protected virtual void OnValidating(ValidationEventArgs e)
		{
			if (Validating != null)
				Validating(this, e);
		}

		/*=========================*/
		#endregion

		#region Public Methods
		/*=========================*/

		/// <summary>
		/// 
		/// </summary>
		/// <param name="parent"></param>
		/// <returns></returns>
		public ValueT GetValue(EntityT parent)
		{
			if (parent == null)
				throw new ArgumentNullException("parent");

			// Try to retrieve current value, assign empty value if not
			object input;
			if (!parent.Values.TryGetValue(this, out input))
				input = this.EmptyValue;

			ValueTranslationEventArgs e = new ValueTranslationEventArgs(parent, this);
			e.Input = input;
			e.Output = input is ValueT ? input : this.DefaultValue;
			OnGetValue(e);

			return (ValueT) e.Output;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="parent"></param>
		/// <param name="value"></param>
		public void SetValue(EntityT parent, ValueT value)
		{
			if (parent.DataState == EntityDataState.Deleted)
				throw new InvalidOperationException("Cannot modify the value of an entity marked for deletion.");

			// Validate the value and raise exception if necessary
			string message;

			// EXCEPTION:
			if (!Validate(parent, value, out message))
				throw new Exception(message);

			// TODO: get last value without reinvoking GetValue
			ValueT previouslyAppliedVal = GetValue(parent);

			ValueTranslationEventArgs e = new ValueTranslationEventArgs(parent, this);
			e.Input = value;
			e.Output = value;
			OnSetValue(e);

			if (e.Cancel)
				return;

			// TODO: Dependency notifications
			if (e.Output == null && previouslyAppliedVal == null)
				return;

			if (e.Output != null && e.Output.Equals(previouslyAppliedVal))
				return;

			// Apply value
			parent.Values[this] = e.Output;

			// Change data state to Modified if it was unchanged
			if (parent.DataState == EntityDataState.Unchanged)
				parent.DataState = EntityDataState.Modified;

			// Raise events
			if (ValueChanged != null)
				ValueChanged(this, new ValueChangedEventArgs(parent, this, previouslyAppliedVal, value));

			if (parent.HasPropertyChangedHandlers)
				parent.OnPropertyChanged(new PropertyChangedEventArgs(this.InlineName));
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="parent"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public bool Validate(Entity parent, object value)
		{
			string m;
			return Validate(parent, value, out m);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="parent"></param>
		/// <param name="value"></param>
		/// <param name="message"></param>
		/// <returns></returns>
		public bool Validate(Entity parent, object value, out string message)
		{
			if (parent != null && !(parent is EntityT))
				throw new ArgumentException("Parent entity must be of type " + EntityType.FullName, "parent");

			if (value != null && !(value is ValueT))
			{
				message = "Value must be of type " + ValueType.FullName;
				return false;
			}

			ValidationEventArgs e = new ValidationEventArgs(parent, this);
			e.Value = value;
			e.Valid = true;
			e.ValidationMessage = null;
			OnValidating(e);

			message = e.ValidationMessage;
			return e.Valid;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="parent"></param>
		/// <returns></returns>
		public bool IsAvailable(Entity parent)
		{
			foreach (Dependency dependency in this.Dependencies)
			{
				if (!dependency.IsActive(parent))
					return false;
			}

			return true;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="parentProperty"></param>
		/// <returns></returns>
		public Dependency GetParentDependency(IEntityProperty parentProperty)
		{
			// Check if there is a direct dependency
			Dependency direct = this.Dependencies[parentProperty];
			if (direct != null)
				return direct;

			foreach (Dependency dep in this.Dependencies)
			{
				// Recursively check the parent dependencies
				Dependency indirect = dep.ParentProperty.GetParentDependency(parentProperty);
				if (indirect != null)
					return indirect;
			}

			// Nothing found
			return null;
		}

		/*=========================*/
		#endregion

		#region IEntityProperty Members
		/*=========================*/

		object IEntityProperty.DefaultValue
		{
			get
			{
				return _defaultValue;
			}
		}

		object IEntityProperty.EmptyValue
		{
			get
			{
				return _emptyValue;
			}
		}

		object IEntityProperty.GetValue(Entity parent)
		{
			return this.GetValue((EntityT) parent);
		}

		void IEntityProperty.SetValue(Entity parent, object value)
		{
			this.SetValue((EntityT) parent, (ValueT) value);
		}

		/*=========================*/
		#endregion
	}

	/// <summary>
	/// Generic class for properties that represent a related entity.
	/// </summary>
	/// <typeparam name="EntityT"></typeparam>
	/// <typeparam name="ReferenceEntityT"></typeparam>
	/// <typeparam name="ReferenceIdentityT"></typeparam>
	public sealed class EntityReferenceProperty<EntityT, ReferenceEntityT, ReferenceIdentityT>: EntityProperty<EntityT, ReferenceEntityT>
		where EntityT: Entity
		where ReferenceEntityT: Entity
	{
		#region Fields

		/*=========================*/

		public readonly EntityProperty<ReferenceEntityT, ReferenceIdentityT> IdentityProperty;

		/*=========================*/
		#endregion

		#region Events
		/*=========================*/

		public event EventHandler<ValueTranslationEventArgs> ReferenceEntityRequired;

		/*=========================*/
		#endregion

		#region Constructors
		/*=========================*/

		public EntityReferenceProperty(EntityProperty<ReferenceEntityT, ReferenceIdentityT> identityProperty): base(null, identityProperty.EmptyValue)
		{
			IdentityProperty = identityProperty;
		}

		/*=========================*/
		#endregion

		#region Internal Methods
		/*=========================*/

		protected override void OnGetValue(ValueTranslationEventArgs e)
		{
			// Get the raw reference ID
			ReferenceIdentityT refID = (ReferenceIdentityT) e.Input;

			if (refID.Equals(IdentityProperty.EmptyValue ))
			{
				// Empty ID means we return null value
				e.Output = null;

				// Remove any reference in case it's still there
				if (e.Entity.References.ContainsKey(this))
					e.Entity.References.Remove(this);
			}
			else
			{
				ReferenceEntityT refEntity = (ReferenceEntityT) e.Entity.References[this];
				if (refEntity == null || !IdentityProperty.GetValue(refEntity).Equals(refID))
				{
					// Raise event for retrieving the entity
					ValueTranslationEventArgs e2 = new ValueTranslationEventArgs(e.Entity, this);
					e2.Input = refID;

					if (ReferenceEntityRequired != null)
						ReferenceEntityRequired(this, e2);

					refEntity = (ReferenceEntityT) e2.Output;
					e.Entity.References[this] = refEntity;
				}

				e.Output = refEntity;
			}

			base.OnGetValue(e);
		}

		protected override void OnSetValue(ValueTranslationEventArgs e)
		{
			ReferenceEntityT refEntity = (ReferenceEntityT) e.Input;

			if (refEntity == null)
			{
				e.Output = IdentityProperty.EmptyValue;

				// Remove any reference in case it's still there
				if (e.Entity.References.ContainsKey(this))
					e.Entity.References.Remove(this);
			}
			else
			{
				e.Output = IdentityProperty.GetValue(refEntity);
				e.Entity.References[this] = refEntity;
			}
		}

		/*=========================*/
		#endregion

		#region Public Methods
		/*=========================*/

		public ReferenceIdentityT GetIdentity(EntityT parent)
		{
			// TODO: GetIdentity
			throw new NotImplementedException();
		}

		public ReferenceIdentityT SetIdentity(EntityT parent, ReferenceIdentityT id)
		{
			// TODO: SetIdentity
			throw new NotImplementedException();
		}

		/*=========================*/
		#endregion

	}
}
