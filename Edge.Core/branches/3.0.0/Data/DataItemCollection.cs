using System;
using System.Collections;
using System.Data;
using System.Data.Common;
using System.Reflection;
using System.Data.Sql;
using System.Data.SqlClient;
using System.Collections.Specialized;
using System.Runtime.Serialization;
using Edge.Core.Data.Proxy;
using System.Collections.Generic;


namespace Edge.Core.Data
{
	/// <summary>
	/// Base class for data-bound collections.
	/// </summary>
	/// 
	/// <remarks>
	/// <para>
	/// DataItemCollection is the base class for all collections that represent
	/// record sets. The items in such derived collections are usually foreign
	/// keys for independent items defined in other tables. For instance,
	/// <see cref="PT.Data.PropertyKeywordCollection"/> enumerates items of type
	/// <see cref="PT.Data.Keyword"/> where in fact the items in the list are only
	/// references to the actual keywords, which exist in their own dedicated table.
	/// </para>
	/// <para>
	/// DataItemCollection supports access by primary key. When <see cref="PrimaryKey"/> is
	/// overrided in a derived class and returns a property that is used as primary key,
	/// the collection behaves as a hashtable, enforcing unique keys and allowing lookup by
	/// primary key.
	/// </para>
	/// </remarks>
	//[CollectionDataContract]
	[Serializable]
	public abstract class DataItemCollection : IList<DataItem>, IDataBoundObject, ISerializable
	{
		#region Constants
		/*=========================*/
		private class Const
		{
			public const string InnerIDColumn = "__innerID";
		}

		/*=========================*/
		#endregion

		#region Fields
		/*=========================*/

		/// <summary>
		/// 
		/// </summary>
		private List<DataItem> _innerList = new List<DataItem>();

		/// <summary>
		/// The internal table used to store the data.
		/// </summary>
		//[DataMember]
		private DataTable _table;

		/// <summary>
		/// The hash table that stores the items by primary key (when enabled).
		/// </summary>
		private Hashtable _hash;

		/// <summary>
		/// The SQL command used to retrieve rows.
		/// </summary>
		private SqlCommand _selectCmd;

		/// <summary>
		/// The SQL command used to add rows.
		/// </summary>
		private SqlCommand _insertCmd;

		/// <summary>
		/// The SQL command used to update rows.
		/// </summary>
		private SqlCommand _updateCmd;

		/// <summary>
		/// The SQL command used to delete rows.
		/// </summary>
		private SqlCommand _deleteCmd;

		/// <summary>
		/// 
		/// </summary>
		//[DataMember]
		private int _actionIndex = -1;

		/*=========================*/
		#endregion

		#region Events
		/*=========================*/
		//public event NotifyCollectionChangedEventHandler CollectionChanged;

		/// <summary>
		/// Raised when the Bind method adds an incoming item to the collection.
		/// </summary>
		public event EventHandler<BoundItemRecievedEventArgs> BoundItemReceived;
		/*=========================*/
		#endregion

		#region Constructors
		/*=========================*/
		
		/// <summary>
		/// Creates a new data collection.
		/// </summary>
		protected DataItemCollection()
		{
			// Create the hashtable if we need to support primary key lookups
			if (this.PrimaryKey != null)
				_hash = new Hashtable();
		}

		/*=========================*/
		#endregion
		
		#region Internal Methods
		/*=========================*/
		
		/// <summary>
		/// Populates the collection with DataItem objects by binding to the data source.
		/// </summary>
		/// 
		/// <remarks>
		/// Calling this method clears the inner table before re-populating the list, which means
		/// items contained in the collection before the binding call are disassociated from the collection
		/// (their <c>DataState</c> changes to <c>DataRowState.Detached</c>). Changes to detached items
		/// will not be saved when the collection is saved (unless they are re-added).
		/// </remarks>
		protected void Bind()
		{
			// Clear the table
			_table.Rows.Clear();

			// Clear items
			_innerList.Clear();
			if (_hash != null)
				_hash.Clear();

			if (!DataManager.ProxyMode)
			{
				// Attach an event handler to the table so we can create DataItems for each row added
				DataRowChangeEventHandler addedHandler = new DataRowChangeEventHandler(_table_RowChanged);
				_table.RowChanged += addedHandler;

				SqlDataAdapter adapter = DataManager.CreateAdapter(this);
				ConnectionKey ckey = null;
				try
				{
					ckey = DataManager.Current.OpenConnection(this);

					// Fill the table
					adapter.Fill(this._table);
				}
				finally
				{
					DataManager.Current.CloseConnection(ckey);
				}

				// Detach the event so it doesn't get raised in future changes
				_table.RowChanged -= addedHandler;

				if (ProxyServer.InProgress)
					ProxyServer.Current.Result[ProxyServer.Current.CurrentAction].AddData("_table", _table);
			}
			else
			{
				ProxyRequestAction action = ProxyClient.Request.AddAction(this, MethodInfo.GetCurrentMethod());
				action.OnComplete = delegate()
				{
					DataTable table = ProxyClient.Result[action].GetData<DataTable>("_table");
					_table = table;

					// Create new items for each row using the existing handler
					foreach (DataRow row in _table.Rows)
						_table_RowChanged(_table, new DataRowChangeEventArgs(row, DataRowAction.Add));
				};
			}

		}
		
		
		/// <summary>
		/// Creates and adds a DataItem for each row added to the table during the adapter's fill.
		/// </summary>
		private void _table_RowChanged(object sender, DataRowChangeEventArgs e)
		{
			// Use inner list so we don't invoke the OnInsert method
			if (e.Action == DataRowAction.Add)
			{
				DataItem newItem = this.NewItem(e.Row);
				_innerList.Add(newItem);

				// Add the item to the hash for primary key support
				if (_hash != null)
					_hash.Add(GetPrimaryKeyValue(newItem), newItem);

				if (BoundItemReceived != null)
					BoundItemReceived(this, new BoundItemRecievedEventArgs(newItem));
			}
		}

		
		/// <summary>
		/// Backs up the fields of every item that is deleted from the table, so that they
		/// remain accessible.
		/// </summary>
		private void _table_RowDeleting(object sender, DataRowChangeEventArgs e)
		{
			for (int i = 0; i < _innerList.Count; i++)
			{
				DataItem item = (DataItem) _innerList[i];
				if (item.Row == e.Row)
					item.BackupFieldValues();

			}
		}

		
		/// <summary>
		/// Creates a new collection-specific item using a row in the 
		/// </summary>
		/// 
		/// <param name="row">
		/// The DataRow from which to retrieve the new item's data.
		/// </param>
		///
		/// <returns>
		/// The new item encapsulating the row.
		/// </returns>
		///
		/// <remarks>
		/// Inheriting classes override this method and return a new instance of
		/// the DataItem-derived class that they enumerate.
		/// </remarks>
		protected virtual DataItem NewItem(DataRow row)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="itemToDuplicate"></param>
		/// <returns></returns>
		protected virtual DataItem NewItem(DataItem itemToDuplicate)
		{
			throw new NotImplementedException();
		}


		/// <summary>
		/// Uses reflection to get the primary key value of the specified item.
		/// </summary>
		/// 
		/// <param name="item">
		/// The item for which to retrieve the primary key value.
		/// </param>
		/// 
		/// <returns>
		/// The primary key of the item.
		/// </returns>
		/// 
		/// <remarks>
		/// Null is returned if primary key lookup isn't supported.
		/// </remarks>
		protected object GetPrimaryKeyValue(DataItem item)
		{
			object retVal;
			if (this.PrimaryKey != null)
				retVal = this.PrimaryKey.GetValue(item, new object[0]);
			else
				retVal = null;

			return retVal;
		}

		
		/// <summary>
		/// Creates and initializes the commands used by the item.
		/// </summary>
		///
		/// <remarks>
		/// This method is called by the base class as well as inheriting classes whenever commands need
		/// to be initialized.
		/// </remarks>
		protected void InitCommands()
		{
			if (DataManager.ProxyMode)
				return;

			OnInitCommands(out _selectCmd, out _insertCmd, out _updateCmd, out _deleteCmd);
		}

		
		/// <summary>
		/// Invoked when commands need to be initialized.
		/// </summary>
		/// 
		/// <param name="selectCmd">The variable that will be assigned the new select command.</param>
		/// <param name="insertCmd">The variable that will be assigned the new insert command.</param>
		/// <param name="updateCmd">The variable that will be assigned the new update command.</param>
		/// <param name="deleteCmd">The variable that will be assigned the new delete command.</param>
		/// 
		/// <remarks>
		///	Derived classes override this method and assign their own commands. The default implementation
		///	throws an exception.
		/// </remarks>
		protected virtual void OnInitCommands(out SqlCommand selectCmd, out SqlCommand insertCmd, out SqlCommand updateCmd, out SqlCommand deleteCmd)
		{
			throw new NotImplementedException();
		}


		/// <summary>
		/// Creates and initializes the internal table.
		/// </summary>
		/// 
		/// <remarks>
		/// <para>
		/// Creates the internal table used to store the records that represent the collection items.
		/// </para>
		/// <para>
		/// This command needs to be called when the data provider does not create the table schema
		/// on it's own, which is the case when creating new empty collections.
		/// </para>
		/// </remarks>
		protected void InitializeTable()
		{
			// Capture row deleting events so we can backup the values
			_table = new DataTable("_table");
			_table.RowDeleting += new DataRowChangeEventHandler(_table_RowDeleting);

			OnInitializeTable();

			if (DataManager.ProxyMode)
				_table.Columns.Add(Const.InnerIDColumn, typeof(int)).AutoIncrement = true;
		}


		/// <summary>
		/// Invoked when the table needs initialization.
		/// </summary>
		/// 
		/// <remarks>
		/// The method's role is to add columns to the table as necessary. Derived classes override
		/// this method and add columns appropriate for that type. The default implementation
		/// generates an exception.
		/// </remarks>
		protected virtual void OnInitializeTable()
		{
			throw new NotImplementedException();
		}

		
		/// <summary>
		/// Handles saving the collection's items to the data source.
		/// </summary>
		/// 
		/// <remarks>
		/// The base implementation of this method uses a DataAdapter with the collection's associated commands
		/// in order to save the contained records to the database. Derived classes can override this method to add additional
		/// checks or operations before and after the save, or to change the saving method altogether.
		/// </remarks>
		protected virtual void OnSave()
		{
			// Don't perform the save if there are no changes
			if (_table.GetChanges() == null)
				return;

			if (!DataManager.ProxyMode)
			{
				// Create an adapter
				SqlDataAdapter adapter = DataManager.CreateAdapter(this);

				// Vars for proxy operations
				DataTable updatedRowsTable = null;
				SqlRowUpdatedEventHandler updatedHander = null;

				if (ProxyServer.InProgress)
				{
					updatedRowsTable = _table.Clone();
					updatedHander = delegate(object sender, SqlRowUpdatedEventArgs e)
					{
						if (e.StatementType != StatementType.Insert && e.StatementType != StatementType.Update)
							return;

						// Store and row that has been inserted or updated
						updatedRowsTable.ImportRow(e.Row);
					};

					adapter.RowUpdated += updatedHander;
				}

				ConnectionKey ckey = null;
				using (adapter)
				{
					try
					{
						ckey = DataManager.Current.OpenConnection(this);
						adapter.Update(_table);
					}
					finally
					{
						DataManager.Current.CloseConnection(ckey);
					}
				}

				// Wrap up the proxy operation
				if (ProxyServer.InProgress)
				{
					adapter.RowUpdated -= updatedHander;
					ProxyServer.Current.Result[ProxyServer.Current.CurrentAction].AddData("updated", updatedRowsTable);
				}
			}
			else
			{
				ProxyRequestAction action = ProxyClient.Request.AddAction(this, MethodInfo.GetCurrentMethod());
				action.OnComplete = delegate()
				{
					DataTable updated = ProxyClient.Result[action].GetData<DataTable>("updated");
					bool usingInnerID = updated.Columns.Contains(Const.InnerIDColumn);
					if (!usingInnerID)
						throw new Exception("not using inner ID!!!");

					// Go over each row and find matching rows
					for (int i = 0; i < updated.Rows.Count; i++)
					{
						bool merged = false;
						DataRow updatedRow = updated.Rows[i];
						object testID = usingInnerID ? updatedRow[Const.InnerIDColumn] : null;
						if (testID is int)
						{
							int innerID = (int) testID;
							DataRow[] rs = _table.Select(String.Format("{0} = {1}", Const.InnerIDColumn, innerID));
							if (rs.Length > 0)
							{
								// If a row with a matching innerID is found, merge the values and accept changes
								DataRow row = rs[0];
								row.ItemArray = updatedRow.ItemArray;
								row.AcceptChanges();
								merged = true;
							}
						}

						// TODO: make sure we shouldn't be clearing the table before adding unidentified rows
						if (!merged)
						{
							// Insert the new row at the same index
							DataRow newRow = _table.NewRow();
							newRow.ItemArray = updatedRow.ItemArray;
							_table.Rows.InsertAt(newRow, i);

							DataItem newItem = NewItem(newRow);
							_innerList.Insert(i, newItem);

							if (_hash != null)
								_hash.Add(GetPrimaryKeyValue(newItem), newItem);
						}
					}
				};
			}
		}

		
		/// <summary>
		/// Handles clearing the collection.
		/// </summary>
		protected virtual void OnClear()
		{
			//DataItem[] items = new DataItem[this.Count];
			for(int i=0; i < _innerList.Count; i++)
			{
				DataItem item = (DataItem) _innerList[i];
				//items[i] = item;
				item.Row.Delete();
			}

			// Clear the has
			if (_hash != null)
				_hash.Clear();
		}


		#region Obsolete
//		/// <summary>
//		/// Handles insert operations.
//		/// </summary>
//		/// <param name="index">The index to insert, if less than the last position throws an exception. </param>
//		/// <param name="value">The DataItem to insert.</param>
//		protected override void OnInsert(int index, object value)
//		{
//			if (!(value is DataItem))
//				throw new ArgumentException("Only DataItems can be added to a " + typeof(DataItemCollection).FullName);
//
//			DataItem item = (DataItem) value;
//			
//			if (this.Contains(item))
//				throw new ArgumentException("Item is already in the collection");
//
//			// We can't insert items since there's no order to the records
//			if (index < List.Count && this.AllowPositioning)
//				throw new ArgumentException("Items can only be added to the end of the collection");
//			
//			// Add the row
//			_table.Rows.Add(item.Row);
//			try
//			{
//				base.OnInsert (index, value);
//			}
//			catch
//			{
//				// Cancel on exception
//				item.Row.Delete();
//				throw;
//			}
//		}
		#endregion

		
		/// <summary>
		/// Handles insert operations.
		/// </summary>
		/// 
		/// <param name="index">The index at which to insert the item. </param>
		/// <param name="value">The DataItem to insert.</param>
		protected virtual void OnInsert(int index, object value)
		{
			if (!(value is DataItem) || value == null)
				throw new ArgumentException("Only a valid DataItem can be added to a DataItemCollection.", "value");

			if (!value.GetType().Equals(this.DataItemType) && !value.GetType().IsSubclassOf(this.DataItemType))
				throw new ArgumentException("Only items of type " + this.DataItemType.FullName + " may be used in a " + this.GetType().FullName + ".");

			// We can't position items if positioning is not allowed
			if (index < List.Count && !this.AllowPositioning)
				throw new ArgumentException("Items can only be added to the end of the collection.");
			
			// Get the value as a data item
			DataItem item = (DataItem) value;

			// Add a new item using the imported row
			if (item.Row.Table != _table || item.ParentCollection != this)
				throw new ArgumentException("Only a DataItem directly associated to this collection can be added. " +
					"Use the Add and Insert method of DataItemCollection instead of the ones exposed by the IList interface.");

			DataRowState stateBeforeAdd = item.Row.RowState;
			if (item.Row.RowState == DataRowState.Deleted)
			{
				// If the item was deleted, undo all changes since last update (we'll restore the current values from the
				// internal backup)
				item.Row.RejectChanges();
			}
			else
			{
				// Otherwise, add the row
				_table.Rows.Add(item.Row);
			}

			// Restore the item's state, because it might have been edited while it was detached (and the values
			// are in the backup hashtable, not in the main row)
			if(stateBeforeAdd == DataRowState.Deleted || stateBeforeAdd == DataRowState.Detached)
				item.RestoreFieldValues();

			// Add the item to the hash if necessary
			if (_hash != null)
				_hash.Add(GetPrimaryKeyValue(item), item);

			//if (CollectionChanged != null)
			//	CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item);
		}

		
		/// <summary>
		/// Handles remove operations.
		/// </summary>
		/// <param name="index">Position at which to remove the item.</param>
		/// <param name="value">The DataItem to remove.</param>
		protected virtual void OnRemove(int index, object value)
		{
			if (!(value is DataItem) || value == null)
				throw new ArgumentException("Only a valid DataItem can be added to a DataItemCollection.", "value");

			if (!value.GetType().Equals(this.DataItemType) && !value.GetType().IsSubclassOf(this.DataItemType))
				throw new ArgumentException("Only items of type " + this.DataItemType.FullName + " may be used in a " + this.GetType().FullName + ".");

			// Remove the item from the list and it's row from the table
			DataItem item = (DataItem) value;

			if (item.Row.Table != _table || item.ParentCollection != this)
				throw new ArgumentException("Item does not belong to this collection.");

			if (_hash != null)
				_hash.Remove(GetPrimaryKeyValue(item));
			
			item.BackupFieldValues();
			item.Row.Delete();
		}

		/// <summary>
		/// Throws a <see cref="System.NotSupportedException"/>.
		/// </summary>
		protected virtual void OnSet(int index, object oldValue, object newValue)
		{
			DataItem newItem = newValue as DataItem;
			if (newItem == null || !DataItemType.IsInstanceOfType(newItem) || newItem.ParentCollection != this)
				throw new InvalidOperationException("Only items already belonging to the collection can be set to new indexes");

			// Remove the new item from it's old position and put it in the new one
			if (newItem.Row.RowState != DataRowState.Deleted && newItem.Row.RowState != DataRowState.Detached)
				this.Remove(newItem);
			
			this.Insert(index, newItem);
		}

		
		/// <summary>
		/// Deletes the rows in the table, updates the database and then re-adds the rows.
		/// </summary>
		/// <remarks>
		/// Used to disassociate a collection from a parent object but allowing the values to be reused.
		/// (used in <see cref="PT.Data.QueryKeywordGroupCollection"/>).
		/// </remarks>
		protected void DeleteFromDB()
		{
			if (DataManager.ProxyMode)
				throw new NotSupportedException("DeleteFromDB currently not supported in proxy mode");

			// Delete all rows
			foreach(DataRow row in _table.Rows)
			{
				row.Delete();
			}

			// Update to DB
			SqlDataAdapter adapter = DataManager.CreateAdapter(this);

			ConnectionKey ckey = null;
			using(adapter)
			{
				try
				{
					ckey = DataManager.Current.OpenConnection(this);

					adapter.Update(_table);
				}
				finally
				{
					DataManager.Current.CloseConnection(ckey);
				}
			}

			// Restore all item rows that are still in the list (not items that were removed
			foreach(DataItem item in this._innerList)
			{
				_table.Rows.Add(item.Row);
				item.RestoreFieldValues();
			}
		}

	
		/*=========================*/
		#endregion

		#region Internal Properties
		/*=========================*/

		protected IList<DataItem> List
		{
			get
			{
				return this;
			}
		}
		
		/// <summary>
		/// Gets the inner table that stores the collection data.
		/// </summary>
		public DataTable InnerTable
		{
			get
			{
				return _table;
			}
		}
		
		
		/// <summary>
		/// Gets the SELECT command used by the adapter/reader.
		/// </summary>
		protected SqlCommand SelectCommand
		{
			get
			{
				return _selectCmd;
			}
		}

		/// <summary>
		/// Gets the INSERT command used by the adapter.
		/// </summary>
		protected SqlCommand InsertCommand
		{
			get
			{
				return _insertCmd;
			}
		}

		
		/// <summary>
		/// Gets the UPDATE command used by the adapter.
		/// </summary>
		protected SqlCommand UpdateCommand
		{
			get
			{
				return _updateCmd;
			}
		}

		
		/// <summary>
		/// Gets the DELETE command used by the adapter.
		/// </summary>
		protected SqlCommand DeleteCommand
		{
			get
			{
				return _deleteCmd;
			}
		}


		/// <summary>
		/// Gets a value indicating whether there is any significance to the order of the elements.
		/// </summary>
		/// 
		/// <remarks>
		/// Defaults to false. Collections that implement ordering of the elements should override this
		/// property and return true.
		/// </remarks>
		protected virtual bool AllowPositioning
		{
			get
			{
				return false;
			}
		}


		/// <summary>
		/// Gets the property used to uniquely identify the items in the collection.
		/// </summary>
		/// 
		/// <remarks>
		/// Used to determine whether
		/// a DataItem represents data that is already present in the collection, regardless of whether the
		/// object itself is part on the internal array or not. Defaults to null, meaning there is no identity
		/// checking and a number of separate DataItem objects that represent the same data can all be added to
		/// the collection (for example, a single keyword can be added multiple times).
		/// </remarks>
		protected virtual PropertyInfo PrimaryKey
		{
			get
			{
				return null;
			}
		}


		/*=========================*/
		#endregion

		#region Public Properties
		/*=========================*/
		
		/// <summary>
		/// Gets the type of DataItem the collection enumerates.
		/// </summary>
		/// 
		/// <remarks>
		/// Inheriting classes must override this property and return a valid System.Type.
		/// The default implementation throws a <see cref="System.NotImplementedException"/>
		/// </remarks>
		public virtual System.Type DataItemType
		{
			get
			{
				throw new NotImplementedException();
			}
		}


		/// <summary>
		/// Gets the item at the specified index.
		/// </summary>
		/// 
		/// <remarks>
		/// Index-based access uses the standard list implementation.
		/// </remarks>
		public DataItem this[int index]
		{
			get
			{
				return this.List[index];
			}
		}


		/// <summary>
		/// Gets the item with the specified primary key value.
		/// </summary>
		/// 
		/// <remarks>
		/// Primary-key access uses a hashcode implementation for faster access.
		/// </remarks>
		protected DataItem this[object value]
		{
			get
			{
				if (this.PrimaryKey == null || _hash == null)
					throw new InvalidOperationException("Collections of type " + this.GetType().FullName + " do not support lookup by primary key");

				DataItem item = _hash[value] as DataItem;

				if (item == null)
				{
					// Nothing found, throw an exception
					throw new IndexOutOfRangeException("Item with specified primary key does not exist in the collection");
				}
				else
				{
					return item;
				}
			}
		}

		/// <summary>
		/// 
		/// </summary>
		public int ProxyActionIndex
		{
			get { return _actionIndex; }
		}


		/*=========================*/
		#endregion

		#region Public Methods
		/*=========================*/
		
		/// <summary>
		/// Submits all the changes made in the collection to the data source.
		/// </summary>
		/// 
		/// <remarks>
		/// When saving the collection, items that have been removed from the collection will be
		/// deleted from the data source, but any references to them will remain valid in memory
		/// (and can be re-added to the collection).
		/// </remarks>
		public void Save()
		{
			this.OnSave();
		}


		/// <summary>
		/// Checks whether an item is contained in the collection.
		/// </summary>
		/// 
		/// <param name="value">The item or item ID to look for.</param>
		/// 
		/// <returns>True if the item or an item representing the same data is contained, otherwise false.</returns>
		public bool Contains(object value)
		{
			DataItem current;
			
			if (_hash != null)
			{
				// If primary key support is enabled, use the hash
				if (value is DataItem)
					current = _hash[GetPrimaryKeyValue(value as DataItem)] as DataItem;
				else
					current = _hash[value] as DataItem;
			}
			else
			{
				// Otherwise lookup by index
				int index = IndexOf(value);
				current = index > 0 ? this[index] : null;
			}
			
			if (current == null)
			{
				// Couldn't find it
				return false;
			}
			else
			if (current.Row.Table != this.InnerTable || current.DataState == DataRowState.Deleted || current.DataState == DataRowState.Detached)
			{
				throw new DataItemInconsistencyException(
					"Item is in the collection but it's internal data row is not in the table");
			}

			return true;
		}

		
		/// <summary>
		/// Checks whether an item is contained in the collection.
		/// </summary>
		/// <param name="item">The item to look for.</param>
		/// <returns>True if the item or an item representing the same data is contained, otherwise false.</returns>
		public bool Contains(DataItem item)
		{
			return Contains((object)item);
		}


		/// <summary>
		/// Removes an item from the collection.
		/// </summary>
		/// 
		/// <param name="value">
		///	A DataItem that is physcially in the collection, a DataItem with a PrimaryKey
		///	belonging to a DataItem in the collection, or a primary key value of a DataItem in the collection.00
		///	</param>
		///	
		/// <returns>The item that was removed.</returns>
		public DataItem Remove(object value)
		{
			// Lookup by index because we want to maintain distinction between identical primary keys
			int index = this.IndexOf(value);

			if (index == -1)
				throw new ArgumentException("The item or an item representing the same data is not in the collection");

			DataItem removed = this[index];
			List.RemoveAt(index);
			return removed;
		}
		

		/// <summary>
		/// Removes an item from the collection.
		/// </summary>
		/// 
		/// <param name="item">
		/// A DataItem that is physcially in the collection or a DataItem with a PrimaryKey
		///	belonging to a DataItem in the collection.
		///	</param>
		///	
		/// <returns>
		/// The item that was removed.
		/// </returns>
		public DataItem Remove(DataItem item)
		{
			return Remove((object)item);
		}


		/// <summary>
		/// Removes the item at the specified location in the collection.
		/// </summary>
		/// <param name="index">The index of the item to remove.</param>
		/// <returns>The item that was removed.</returns>
		public DataItem RemoveAt(int index)
		{
			DataItem item = (DataItem) List[index];
			List.RemoveAt(index);
			return item;
		}
		

		/// <summary>
		/// Adds an item to the collection.
		/// </summary>
		/// 
		/// <param name="item">The item to add.</param>
		/// 
		/// <returns>
		/// The item that was added.
		/// </returns>
		public DataItem Add(DataItem item, bool duplicateItem)
		{
			// Check wheter item is already in the collection
			if (this.Contains(item))
				throw new ArgumentException("The item or an item representing identical data is already in the collection.");

			DataItem itemToAdd = item;
			if (item.Row.Table != _table)
			{
				if (duplicateItem)
				{
					itemToAdd = this.NewItem(item);
				}
				else
					item.SetParentCollection(this);
			}

			List.Add(itemToAdd);
			return itemToAdd;
		}


		/// <summary>
		/// 
		/// </summary>
		/// <param name="item"></param>
		/// <returns></returns>
		public DataItem Add(DataItem item)
		{
			return Add(item, false);
		}

		
		/// <summary>
		/// Inserts an item into the collection at the specified position.
		/// </summary>
		/// 
		/// <param name="index">The position at which to insert the item.</param>
		/// <param name="item">The item to insert.</param>
		/// 
		/// <returns>
		/// The item that was inserted.
		/// </returns>
		/// 
		/// <remarks>
		/// This method is marked as protected because by default DataItemCollection-derived
		/// types do not support positioning. Inheriting classes that do should override <see cref="AllowPositioning"/>
		/// and return <c>true</c>, and define a public Insert method.
		/// </remarks>
		protected DataItem Insert(int index, DataItem item)
		{
			// Check wheter item is already in the collection
			if (this.Contains(item))
				throw new ArgumentException("The item or an item representing identical data is already in the collection.");

			DataItem itemToAdd = item;
			if (item.Row.Table != _table)
				itemToAdd = this.NewItem(item.Row);

			List.Insert(index, itemToAdd);
			return itemToAdd;
		}

		/// <summary>
		/// Gets the index of the specified item or of the item with the same primay key.
		/// </summary>
		/// 
		/// <param name="value">
		/// An item in the collection, an item with an identical primary key, or a primary key value.
		/// </param>
		/// 
		/// <returns>
		/// The item's index.
		/// </returns>
		public int IndexOf(object value)
		{
			// Can't use nulls
			if (value == null)
				return -1;

			int index = -1;
			
			// Search by object reference
			if (value is DataItem)
			{
				index = List.IndexOf((DataItem)value);
			}

			// Search by primary key
			if (index == -1 && this.PrimaryKey != null)
			{
				object primaryKey = null;

				if (value is DataItem)
				{
					primaryKey = GetPrimaryKeyValue(value as DataItem);
				}
				else
				{
					primaryKey = value;
				}

				for(int i = 0; i < List.Count; i++)
				{
					if (primaryKey.Equals(GetPrimaryKeyValue(this[i])))
					{
						index = i;
						break;
					}	
				}
			}

			return index;
		}


		/// <summary>
		/// Gets the index of the specified item or of the item with the same primay key.
		/// </summary>
		/// <param name="item">
		/// An item in the collection or an item with an identical primary key.
		/// </param>
		/// 
		/// <returns>
		/// The item's index.
		/// </returns>
		public int IndexOf(DataItem item)
		{
			return IndexOf((object)item);
		}


		/// <summary>
		/// Sorts the items in the collection.
		/// </summary>
		/// <param name="comparer"></param>
		public virtual void Sort(IComparer<DataItem> comparer)
		{
			_innerList.Sort(comparer);
			
		}

		/// <summary>
		/// Gets a copy of the item's internal data structure.
		/// </summary>
		/// <returns></returns>
		public DataTable InnerTableCopy()
		{
			return this._table.Copy();
		}

		/*=========================*/
		#endregion
		
		#region IDataBoundObject Members
		/*=========================*/

		/// <summary>
		/// SelectCommand implementation.
		/// </summary>
		SqlCommand IDataBoundObject.SelectCommand
		{
			get
			{
				return this.SelectCommand;
			}
		}
		
		
		/// <summary>
		/// InsertCommand implementation.
		/// </summary>
		SqlCommand IDataBoundObject.InsertCommand
		{
			get
			{
				return this.InsertCommand;
			}
		}
		
		
		/// <summary>
		/// UpdateCommand implementation.
		/// </summary>
		SqlCommand IDataBoundObject.UpdateCommand
		{
			get
			{
				return this.UpdateCommand;
			}
		}
		
		
		/// <summary>
		/// DeleteCommand implementation.
		/// </summary>
		SqlCommand IDataBoundObject.DeleteCommand
		{
			get
			{
				return this.DeleteCommand;
			}
		}

		
		/// <summary>
		/// Bind implementation.
		/// </summary>
		void IDataBoundObject.Bind()
		{
			this.Bind();
		}

		
		/// <summary>
		/// Save implementation.
		/// </summary>
		void IDataBoundObject.Save()
		{
			this.Save();
		}


		/*=========================*/
		#endregion

		#region Serialization

		protected DataItemCollection(SerializationInfo info, StreamingContext context)
		{
			DataTable table = (DataTable) info.GetValue("_table", typeof(DataTable));

			if (table == null)
				//InitializeTable();
				throw new SerializationException("Failed to deserialize DataItemCollection; a deserialized table was not found.");

			_table = table;
			_actionIndex = (int) info.GetValue("_actionIndex", typeof(int));

			// Add an identity column for proxy operation
			if (!_table.Columns.Contains(Const.InnerIDColumn))
				_table.Columns.Add(Const.InnerIDColumn, typeof(Int32)).AutoIncrement = true;

			foreach (DataRow row in _table.Rows)
			{
				DataItem newItem = this.NewItem(row);
				this._innerList.Add(newItem);

				// Add the item to the hash for primary key support
				if (_hash != null)
					_hash.Add(GetPrimaryKeyValue(newItem), newItem);
			}

			DeserializeData(info);
			InitCommands();
		}

		void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
		{
			// TODO: allow option to define the table data being serialized

			// Store only the inner table on serialization 
			info.AddValue("_table", _table);
			info.AddValue("_actionIndex", _actionIndex);

			SerializeData(info);
		}

		protected virtual void SerializeData(SerializationInfo info)
		{
		}

		protected virtual void DeserializeData(SerializationInfo info)
		{
		}

		/*
		[OnDeserialized]
		private void OnDeserialized(StreamingContext context)
		{
			// Add an identity column for proxy operation
			if (!_table.Columns.Contains(Const.InnerIDColumn))
				_table.Columns.Add(Const.InnerIDColumn, typeof(Int32)).AutoIncrement = true;

			foreach (DataRow row in _table.Rows)
			{
				DataItem newItem = this.NewItem(row);
				this._innerList.Add(newItem);

				// Add the item to the hash for primary key support
				if (_hash != null)
					_hash.Add(GetPrimaryKeyValue(newItem), newItem);
			}

			InitCommands();
		}
		*/

		#endregion

		#region IList<DataItem> Members

		int IList<DataItem>.IndexOf(DataItem item)
		{
			return _innerList.IndexOf(item);
		}

		void IList<DataItem>.Insert(int index, DataItem item)
		{
			OnInsert(index, item);
			_innerList.Insert(index, item);
		}

		void IList<DataItem>.RemoveAt(int index)
		{
			OnRemove(index, _innerList[index]);
			_innerList.RemoveAt(index);
		}

		DataItem IList<DataItem>.this[int index]
		{
			get
			{
				return _innerList[index];
			}
			set
			{
				OnSet(index, _innerList[index], value);
			}
		}

		#endregion

		#region ICollection<DataItem> Members

		void ICollection<DataItem>.Add(DataItem item)
		{
			OnInsert(this.Count, item);
			_innerList.Add(item);
		}

		public void Clear()
		{
			OnClear();
			_innerList.Clear();
		}

		public void CopyTo(DataItem[] array, int arrayIndex)
		{
			_innerList.CopyTo(array, arrayIndex);
		}

		public int Count
		{
			get { return _innerList.Count; }
		}

		bool ICollection<DataItem>.IsReadOnly
		{
			get { return false; }
		}

		bool ICollection<DataItem>.Remove(DataItem item)
		{
			OnRemove(IndexOf(item), item);
			return _innerList.Remove(item);
		}

		#endregion

		#region IEnumerable<DataItem> Members

		IEnumerator<DataItem> IEnumerable<DataItem>.GetEnumerator()
		{
			return _innerList.GetEnumerator();
		}

		#endregion

		#region IEnumerable Members

		IEnumerator IEnumerable.GetEnumerator()
		{
			return ((IEnumerable)_innerList).GetEnumerator();
		}

		#endregion
	}

	public class BoundItemRecievedEventArgs: EventArgs
	{
		public readonly DataItem ReceivedItem;
		public BoundItemRecievedEventArgs(DataItem item)
		{
			ReceivedItem = item;
		}
	}
}
