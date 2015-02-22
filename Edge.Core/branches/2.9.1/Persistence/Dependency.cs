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
	public enum DependencyInclusionType
	{
		Inclusive,
		Exclusive
	}

	/// <summary>
	/// 
	/// </summary>
	public class Dependency
	{
		#region Fields
		/*=========================*/

		public readonly DependencyInclusionType InclusionType;
		public readonly IEntityProperty ParentProperty;
		public readonly List<object> AcceptedValues;
		internal EventHandler<ValueChangedEventArgs> ValueChangedHandler;

		/*=========================*/
		#endregion

		#region Constructors
		/*=========================*/

		/// <summary>
		/// 
		/// </summary>
		/// <param name="parentProperty"></param>
		/// <param name="inclusionType"></param>
		/// <param name="acceptedValues"></param>
		internal Dependency(IEntityProperty parentProperty, DependencyInclusionType inclusionType, object[] acceptedValues)
		{
			InclusionType = inclusionType;
			ParentProperty = parentProperty;
			AcceptedValues = new List<object>(acceptedValues);
			ValueChangedHandler = new EventHandler<ValueChangedEventArgs>(ParentValueChanged);
		}

		/*=========================*/
		#endregion

		#region Event handlers
		/*=========================*/

		/// <summary>
		/// Raised when the value of the parent property has been changed. Handles raising the
		/// DependencyStateChanged event in the parent entity.
		/// </summary>
		internal void ParentValueChanged(object sender, ValueChangedEventArgs e)
		{
			bool isActive = this.IsActive(e.Entity);
			if (e.Entity.DependencyStates[ParentProperty] == isActive)
				return;
		
			// Remember the new state
			e.Entity.DependencyStates[ParentProperty] = isActive;
	
			// Raise event
			if (e.Entity.HasDependencyStateChangedHandlers)
				e.Entity.OnDependencyStateChanged(new DependencyStateChangedEventArgs(e.Entity, this.ParentProperty, this, isActive));
		}

		/*=========================*/
		#endregion

		#region Public Methods
		/*=========================*/

		public bool IsActive(Entity parent)
		{
			if (parent.GetType() != ParentProperty.EntityType && !parent.GetType().IsSubclassOf(parent.GetType()))
				throw new ArgumentException("Parent entity must be of same type as property's parent type", "parent");

			if (!ParentProperty.IsAvailable(parent))
				return false;

			bool contains = AcceptedValues.Contains(ParentProperty.GetValue(parent));
			return
				(InclusionType == DependencyInclusionType.Inclusive && contains) ||
				(InclusionType == DependencyInclusionType.Exclusive && !contains);
		}

		/*=========================*/
		#endregion

	}

	/// <summary>
	/// 
	/// </summary>
	public class DependencyTable: IEnumerable<Dependency>
	{
		#region Fields
		/*=========================*/

		private IEntityProperty _dependentProperty;
		private Dictionary<IEntityProperty, Dependency> _dependencies;

		/*=========================*/
		#endregion

		#region Public Properties
		/*=========================*/

		/// <summary>
		/// 
		/// </summary>
		/// <param name="property"></param>
		/// <returns></returns>
		public Dependency this[IEntityProperty property]
		{
			get
			{
				return _dependencies[property];
			}
		}

		/*=========================*/
		#endregion

		#region Public Methods
		/*=========================*/

		/// <summary>
		/// 
		/// </summary>
		/// <param name="property"></param>
		/// <param name="inclusionType"></param>
		/// <param name="acceptedValues"></param>
		/// <returns></returns>
		public Dependency Add(IEntityProperty property, DependencyInclusionType inclusionType, object[] acceptedValues)
		{
			if (_dependencies.ContainsKey(property))
				throw new ArgumentException(String.Format("A dependency already exists on property {0}.", property.InlineName), "property");

			// Circular dependency check
			if (property.GetParentDependency(_dependentProperty) != null)
				throw new DependencyException(String.Format("Cannot add a dependency on {0} because it is in itself dependent on the current property {1}.", property.InlineName, _dependentProperty.InlineName));

			Dependency newDep = new Dependency(property, inclusionType, acceptedValues);
			_dependencies.Add(property, newDep);
			property.ValueChanged += newDep.ParentValueChanged;
			
			return newDep;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="property"></param>
		public void Remove(IEntityProperty property)
		{
			if (!_dependencies.ContainsKey(property))
				throw new InvalidOperationException("There is no dependency for the specified property.");

			Dependency dep = _dependencies[property];
			_dependencies.Remove(property);

			// Release handler
			property.ValueChanged -= dep.ParentValueChanged;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="dependency"></param>
		public void Remove(Dependency dependency)
		{
			Remove(dependency.ParentProperty);
		}

		/*=========================*/
		#endregion

		#region IEnumerable<Dependency> Members

		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		public IEnumerator<Dependency> GetEnumerator()
		{
			return _dependencies.Values.GetEnumerator();
		}

		#endregion

		#region IEnumerable Members

		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return ((IEnumerable) _dependencies).GetEnumerator();
		}

		#endregion
	}
}
