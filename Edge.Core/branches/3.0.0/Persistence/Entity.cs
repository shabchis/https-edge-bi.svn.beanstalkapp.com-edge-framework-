using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Data;
using System.ComponentModel;
using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Reflection;
using System.Runtime.Serialization;

namespace Eggplant.Persistence
{
	public interface IPersistable
	{
		
	}

	public enum EntityDataState
	{
		Unchanged,
		Modified,
		Added,
		Deleted,
		Detached
	}

	public enum EntityMode
	{
	    Idle,
	    BeingEdited,
	    Saving,
		Loading
	}

	/// <summary>
	/// Base class for all entities.
	/// </summary>
	[Serializable]
	public abstract class Entity: INotifyPropertyChanged, IEditableObject, ISerializable
	{
		#region Fields
		/*=========================*/

		internal readonly Dictionary<IEntityProperty, bool> DependencyStates;
		internal readonly Dictionary<IEntityProperty, Entity> References;

		IEntityList _parentList;

		EntityMode _mode = EntityMode.Idle;
		EntityDataState _dataState = EntityDataState.Detached;

		Dictionary<IEntityProperty, object> _currentValues;
		Dictionary<IEntityProperty, object> _originalValues;
		Dictionary<IEntityProperty, object> _editModeValues;

		/*=========================*/
		#endregion

		#region Events
		/*=========================*/

		/// <summary>
		/// 
		/// </summary>
		public event PropertyChangedEventHandler PropertyChanged;

		/// <summary>
		/// 
		/// </summary>
		public event EventHandler<DependencyStateChangedEventArgs> DependencyStateChanged;

		public event EventHandler EntityModeChanged;
		public event EventHandler DataStateChanged;
		public event EventHandler ChangesAccepted;
		public event EventHandler ChangesRejected;

		/*=========================*/
		#endregion

		public Entity()
		{
			DependencyStates = new Dictionary<IEntityProperty,bool>();
			References = new Dictionary<IEntityProperty,Entity>();
			_currentValues = new Dictionary<IEntityProperty,object>();
		}

		#region Internal Methods
		/*=========================*/
		/// <summary>
		/// 
		/// </summary>
		/// <param name="e"></param>
		internal void OnDependencyStateChanged(DependencyStateChangedEventArgs e)
		{
			if (DependencyStateChanged != null)
				DependencyStateChanged(this, e);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="e"></param>
		internal void OnPropertyChanged(PropertyChangedEventArgs e)
		{
			if (PropertyChanged != null)
				PropertyChanged(this, e);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="original"></param>
		/// <returns></returns>
		internal Dictionary<IEntityProperty, object> Values
		{
			get
			{
				return _currentValues;
			}
		}

		/// <summary>
		/// 
		/// </summary>
		internal bool HasDependencyStateChangedHandlers
		{
			get { return DependencyStateChanged != null; }
		}

		/// <summary>
		/// 
		/// </summary>
		internal bool HasPropertyChangedHandlers
		{
			get { return PropertyChanged != null; }
		}


		/*=========================*/
		#endregion

		#region Public Properties
		/*=========================*/

		/// <summary>
		/// 
		/// </summary>
		public IEntityList ParentList
		{
			get
			{
				return _parentList;
			}
			internal set
			{
				_parentList = value;
			}
		}


		public EntityMode EntityMode
		{
			get
			{
				return _mode;
			}
			private set
			{
				EntityMode before = _mode;
				if (before == value)
					return;

				_mode = value;
				
				// TODO: EventArgs
				if (EntityModeChanged != null)
					EntityModeChanged(this, EventArgs.Empty); //before, value);
			}
		}


		/// <summary>
		/// 
		/// </summary>
		public EntityDataState DataState
		{
			get
			{
				return _dataState;
			}
			internal set
			{
				ThrowExceptionIfNotReady();

				// Assuming the data state is being changed by a trusted internal source
				EntityDataState before = _dataState;
				if (before == value)
					return;

				if (value == EntityDataState.Modified)
				{
					// When changing to modified, duplicate current values
					_originalValues.Clear();
					foreach(KeyValuePair<IEntityProperty, object> entry in _currentValues)
						_originalValues.Add(entry.Key, entry.Value);
				}
				else if (value == EntityDataState.Detached || value == EntityDataState.Unchanged)
				{
					// When returning to unchanged or detaching, clear original values
					_originalValues.Clear();
				}

				// Raise event
				_dataState = value;

				// TODO: EventArgs
				if (DataStateChanged != null)
					DataStateChanged(this, EventArgs.Empty); //before, _dataState);
			}
		}

		/// <summary>
		/// Gets a value indicating whether any values were changed since the last AcceptChanges
		/// </summary>
		public bool HasValueChanges
		{
			get
			{
				return _originalValues != null;
			}
		}
		
		/*=========================*/
		#endregion

		#region Public Methods
		/*=========================*/

		public void AcceptChanges()
		{
			ThrowExceptionIfNotReady();

			if (DataState == EntityDataState.Unchanged)
				return;

			if (DataState == EntityDataState.Detached)
				throw new InvalidOperationException("Cannot accept changes for a detached entity.");

			EntityDataState beforeState = DataState;
			if (beforeState == EntityDataState.Deleted)
				DataState = EntityDataState.Detached;
			else
				DataState = EntityDataState.Unchanged;

			// TODO: proper event args
			if (ChangesAccepted != null)
				ChangesAccepted(this, EventArgs.Empty); // send beforeState

		}

		public void RejectChanges()
		{
			ThrowExceptionIfNotReady();
	
			if (DataState == EntityDataState.Unchanged)
				return;

			if (DataState == EntityDataState.Detached || DataState == EntityDataState.Added)
				throw new InvalidOperationException("Cannot reject changes for an added or detached entity.");

			DataState = EntityDataState.Unchanged;

			if (ChangesRejected != null)
				ChangesRejected(this, EventArgs.Empty);
		}

		/// <summary>
		/// 
		/// </summary>
		public void Save()
		{
			ThrowExceptionIfNotReady();

			if (this.ParentList != null)
				throw new InvalidOperationException("Saving is not allowed for an entity that was created as part of a list.");

			this.EntityMode = EntityMode.Saving;

			OnBeforeSave();

			//PersistenceCommand cmd = Commands[this.DataState];
			//cmd.Execute(this, delegate()
			//{
			//    this.AcceptChanges();
			//    this.State = EntityState.Ready;
			//});

			this.EntityMode = EntityMode.Idle;
		}

		protected virtual void OnBeforeSave()
		{
		}

		/// <summary>
		/// 
		/// </summary>
		public void Delete()
		{
			ThrowExceptionIfNotReady();
			this.DataState = EntityDataState.Deleted;
			Save();
		}

		public void BeginEdit()
		{
			ThrowExceptionIfNotReady();

			EntityMode = EntityMode.BeingEdited;
			_editModeValues = new Dictionary<IEntityProperty, object>(_currentValues);
		}

		public void CancelEdit()
		{
			_currentValues = _editModeValues;
			_editModeValues = null;
			EntityMode = EntityMode.Idle;
		}

		public void EndEdit()
		{
			_editModeValues = null;
			EntityMode = EntityMode.Idle;
		}

		private void ThrowExceptionIfNotReady()
		{
			if (_mode != EntityMode.Idle)
				throw new InvalidOperationException("Cannot perform this operation while entity is not in a ready state.");
		}

		/*=========================*/
		#endregion

		public static IEntityProperty[] GetPropertiyDefinitions(Type entityType)
		{
			FieldInfo[] fields = entityType.GetFields(BindingFlags.Static);
			List<IEntityProperty> props = new List<IEntityProperty>();
			foreach (FieldInfo field in fields)
			{
				if (field.DeclaringType.IsSubclassOf(typeof(IEntityProperty)))
					props.Add((IEntityProperty)field.GetValue(null));
			}

			return props.ToArray();
		}

		// Override this in generated classes to avoid reflection
		public virtual IEntityProperty[] PropertyDefinitions
		{
			get { return GetPropertiyDefinitions(this.GetType()); }
		}

		#region ISerializable Members

		protected Entity(SerializationInfo info, StreamingContext context)
		{
		}

		public void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			throw new NotImplementedException();
		}

		#endregion
	}
}
