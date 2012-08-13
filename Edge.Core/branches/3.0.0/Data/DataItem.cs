using System;
using System.Collections;
using System.Data;
using System.Data.Common;
using System.Data.Sql;
using System.Data.SqlClient;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.Serialization;
using Edge.Core.Data.Proxy;


namespace Edge.Core.Data
{
	/// <summary>
	/// Base class for individual data bound objects.
	/// </summary>
	/// 
	/// <remarks>
	/// <para>
	///	<b>DataItem</b> is the base for all objects that represent individual records in the database.
	///	The object supports two main modes of operation: independent and collection-owned. Independent
	///	mode allows the item to be loaded, saved or deleted directly using the constructor and the Save
	///	and Delete methods. In collection-owned mode, the item is treated as a member of a collection,
	///	and it's existence depends on it's belonging to a collection - calling Save on the owner collection will
	///	either add, update or remove the item from the collection.
	///	This allows the same class to be used both for accessing and modifying an entity definition and for referencing
	///	an entity as part of a list belonging to a different entity. For example, the <see cref="Image"/> class
	///	represents an image entry defintion when in indpendent-mode but represents the position of an image in a lightbox
	///	when it is used in conjunction with <see cref="Lightbox.Images"/>.
	///	</para>
	///	<para>
	///	In independent mode, the item contains an internal <see cref="DataTable"/> with one row, that in turn contains all
	///	the item's fields. In this case the item is responsible for calling stored procedures that synchronize it with
	///	the database. In collection-owned mode, however, the item does not contain any data of it's own but simply
	///	references a row in the owner collection's DataTable, that can include any number of rows (the other items
	///	in the collection). In this case, the item cannot synchronize itself with the database - the owner collection
	///	synchronizes the entire list at once.
	///	</para>
	///	<para>
	///	Objects deriving from <b>DataItem</b> may be created in real-time with records that contain less data than expected (i.e.,
	///	with some of the expected fields missing); the DataItem design will recognize this and retrieve the missing data when it
	///	is requested using the internal <see cref="GetField"/> method (usually by a property that maps to the field).
	///	</para>
	/// </remarks>
	[Serializable]
	public abstract class DataItem: IDataBoundObject, INotifyPropertyChanged, ISerializable
	{
		#region Fields
		/*=========================*/

		/// <summary>
		/// The internal table that contains _row when it is created independently, or _expandedRow when missing fields have been
		/// retrieved for a collection-based item.
		/// </summary>
		//[DataMember]
		private DataTable _table;

		/// <summary>
		/// The internal object that contains the item data.
		/// </summary>
		private DataRow _row;

		/// <summary>
		/// Stores column names that were not present in the data row/record when the object was instantiated.
		/// </summary>
		//[DataMember]
		private ArrayList _missingFields;

		/// <summary>
		/// The object that contains field data that was missing when the object was first created from a collection
		/// and was later retrieved.
		/// </summary>
		private DataRow _retrievedFieldRow;

		/// <summary>
		/// Used to store and provide access to field values when the main row isn't available
		/// </summary>
		//[DataMember]
		private Hashtable _backupFieldValues;

		/// <summary>
		/// The collection that created the item.
		/// </summary>
		private DataItemCollection _owner;

		/// <summary>
		/// The SQL command used to load an independent item.
		/// </summary>
		private SqlCommand _selectCmd;

		/// <summary>
		/// The SQL command used to insert the new item.
		/// </summary>
		private SqlCommand _insertCmd;

		/// <summary>
		/// The SQL command used to individually update the item.
		/// </summary>
		private SqlCommand _updateCmd;

		/// <summary>
		/// The SQL command used to delete the item.
		/// </summary>
		private SqlCommand _deleteCmd;

		/// <summary>
		/// 
		/// </summary>
		private bool _notifyOnPropertyChanged;

		/// <summary>
		/// 
		/// </summary>
		[DataMember]
		private int _actionIndex = -1;

		/*=========================*/
		#endregion

		#region Constructors
		/*=========================*/
		
		/// <summary>
		/// Initializes an empty item.
		/// </summary>
		protected internal DataItem()
		{
			InitializeTable();
			InitCommands();
		}


		/// <summary>
		/// Initializes an item with existing row data, either collection-based on independent mode.
		/// </summary>
		/// 
		/// <param name="externalRow">
		///	The row containing the item's data.
		/// </param>
		/// 
		/// <param name="owner">
		/// The collection that is creating the item. If null, the item is created as an independent item.
		/// </param>
		/// 
		/// <param name="copyLocal">
		/// If false, data is not copied into the internal table even if there is no owner specified.
		/// </param>
		/// 
		/// <remarks>
		/// <para>
		/// If externalRow does not belong to the owner's InnerTable, the data will be imported into
		/// the owner's table as a new row (or imported into the item's independent table if no owner is specified).
		/// </para>
		/// <para>
		/// Fields that exist in the independent version of the item but do not exist in the external row will be
		/// marked as "missing" so that if they are referenced in code their value will be retrieved on-demand.
		/// </para>
		/// </remarks>
		protected internal DataItem(DataRow externalRow, DataItemCollection owner, bool copyLocal): this()
		{
			// If no external row is specified, use a new empty row from the owner or the inner table
			if (externalRow == null)
				externalRow = owner != null ? owner.InnerTable.NewRow() : InnerTable.NewRow();
			
			// Collect missing column names
			_missingFields = new ArrayList();

			// Row data needs to be imported since we're not attaching to a collection
			if (copyLocal && (owner == null || externalRow.Table != owner.InnerTable))
			{
				ImportIntoOwner(externalRow, owner);
			}
			else
			{
				_row = externalRow;
				_owner = owner;

				// Look for missing fields
				foreach(DataColumn column in _table.Columns)
				{
					if (_row.Table.Columns.IndexOf(column.ColumnName) < 0)
						_missingFields.Add(column.ColumnName);
				}
			}

			if (!copyLocal || _owner != null)
			{
				// We don't need the table any more since we have an owner collection
				_table.Dispose();
				_table = null;
			}

		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="externalRow"></param>
		/// <param name="owner"></param>
		protected internal DataItem(DataRow externalRow, DataItemCollection owner): this(externalRow, owner, true )
		{
		}
		
		/// <summary>
		/// Creates an independent item using data from an existing row.
		/// </summary>
		/// 
		/// <param name="row">
		/// The row from which to import data.
		/// </param>
		protected internal DataItem(DataRow row): this(row, null)
		{
		}


		/// <summary>
		/// Creates an independent item using data from an IDataRecord object (usually a DataReader).
		/// </summary>
		/// 
		/// <param name="record">
		/// The record from which to import data.
		/// </param>
		protected internal DataItem(IDataRecord record): this()
		{
			if (record == null)
				throw new ArgumentNullException("record");

			// Import each field from the record
			for(int i = 0; i < _row.Table.Columns.Count; i++)
			{
				string columnName = _row.Table.Columns[i].ColumnName;
				
				// Find the matching field index in the record (by comparing name)
				int fieldIndex = -1;
				for (int j = 0; j < record.FieldCount; j++)
				{
					if (record.GetName(j) == columnName)
					{
						fieldIndex = j;
						break;
					}
				}

				if (fieldIndex > -1)
				{
					// If a matching field was found in the external record, apply the value to the row
					object val = record.GetValue(fieldIndex);
					_row[i] = val == null ? DBNull.Value : val;
				}
				else
				{
					// The field does not exist in the external record, so mark it as missing
					if (_missingFields == null)
						_missingFields = new ArrayList();

					_missingFields.Add(columnName);
				}
			}
		}

		
		/// <summary>
		/// Creates an independent item using values from an array.
		/// </summary>
		/// 
		/// <param name="values">
		/// An array of values to insert into the DataItem's internal columns. The order/type
		/// of the values must match the order/type of the item's independent fields.
		/// </param>
		protected internal DataItem(object[] values): this()
		{
			if (values == null)
				throw new ArgumentNullException("values");

			for (int i = 0; i < Row.Table.Columns.Count && i < values.Length; i++)
			{
				try
				{
					// Try to set the value to the field
					Row[i] = values[i];
				}
				catch(InvalidCastException ex)
				{
					throw new ArgumentException("The values in the value list do not match the DataItem's columns.", "values", ex);
				}
			}

			// There were less values than columns, so add the rest of the fields as missing fields
			if (values.Length < Row.Table.Columns.Count)
			{
				_missingFields = new ArrayList();
				for (int i = values.Length; i < Row.Table.Columns.Count; i++)
				{
					_missingFields.Add(Row.Table.Columns[i].ColumnName);
				}
			}
		}
		
		/*=========================*/
		#endregion

		#region Internal Properties
		/*=========================*/
		
		/// <summary>
		/// Gets the row containing the item's data.
		/// </summary>
		protected internal DataRow Row
		{
			get
			{
				return _row;
			}
		}


		/// <summary>
		/// Gets the internal table used to manage independently created items.
		/// </summary>
		protected internal DataTable InnerTable
		{
			get
			{
				return _table;
			}
		}

		
		/// <summary>
		/// Gets the select command associated with the object.
		/// </summary>
		protected internal SqlCommand SelectCommand
		{
			get
			{
				return _selectCmd;
			}
		}

		protected internal SqlCommand InsertCommand
		{
			get
			{
				return _insertCmd;
			}
		}


		/// <summary>
		/// Gets the SQL command used to update the item in independent mode.
		/// </summary>
		protected internal SqlCommand UpdateCommand
		{
			get
			{
				return _updateCmd;
			}
		}

		
		/// <summary>
		/// Gets the SQL command used to delete the item.
		/// </summary>
		protected internal SqlCommand DeleteCommand
		{
			get
			{
				return _deleteCmd;
			}
		}


		/// <summary>
		/// Contains column names that were not present in the data row/record when the object was instantiated.
		/// </summary>
		protected internal ArrayList MissingFields
		{
			get
			{
				return _missingFields;
			}
		}

		/*=========================*/
		#endregion

		#region Internal Methods
		/*=========================*/

		/// <summary>
		/// Fills the item with data retrieved from the data source using the select command.
		/// </summary>
		protected void Bind()
		{
			// Cannot bind when the table is not managed inside the item
			if (_owner != null)
				throw new InvalidOperationException("Cannot independently bind an item that is part of a collection");

			if (DataManager.ProxyMode)
			{
				ProxyRequestAction action = ProxyClient.Request.AddAction(this, MethodInfo.GetCurrentMethod());
				action.OnComplete = delegate()
				{
					DataTable table = ProxyClient.Result[action].GetData<DataTable>("_table");
					_table = table;
					_row = table.Rows[0];

					// TODO: deal with any field backups and shit
				};
			}
			else
			{
				// Initialize the table
				if (_table == null)
				{
					_table = new DataTable("_table");
				}
				else
				{
					_table.Rows.Clear();
				}

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

				if (_table.Rows.Count < 1)
					throw new DataException("Binding operation did not return data");

				_row = _table.Rows[0];

				if (ProxyServer.InProgress)
					ProxyServer.Current.Result[ProxyServer.Current.CurrentAction].AddData("_table", _table);
			}
		}

		
		/// <summary>
		/// Creates and initializes the commands used by the item to synchronize with the data source.
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
		/// <param name="updateCmd">The variable that will be assigned the new update command.</param>
		/// <param name="deleteCmd">The variable that will be assigned the new delete command.</param>
		/// 
		/// <remarks>
		///	Derived classes override this method and assign their own commands. The default implementation
		///	assigns null to all three.
		/// </remarks>
		protected virtual void OnInitCommands(out SqlCommand selectCmd, out SqlCommand insertCmd, out SqlCommand updateCmd, out SqlCommand deleteCmd)
		{
			selectCmd = null;
			insertCmd = null;
			updateCmd = null;
			deleteCmd = null;
		}


		/// <summary>
		/// Creates and initializes the internal table and row.
		/// </summary>
		/// 
		/// <remarks>
		/// <para>
		/// Creates the internal table containing the single row used for storing an independently
		/// created item's data, or for storing retrieved missing fields for a collection-owned item.
		/// </para>
		/// <para>
		/// This command needs to be called when the data provider does not create the table schema
		/// on it's own, which is the case when creating independent items, either empty or from imported rows.
		/// Alternatively, the table is created when initialized in collection-owned mode in order to identify
		/// fields missing in the collection's table.
		/// </para>
		/// </remarks>
		protected void InitializeTable()
		{
			_table = new DataTable("_table");
			OnInitializeTable();
			_row = _table.NewRow();
			_table.Rows.Add(_row);
		}


		/// <summary>
		/// Invoked when the table needs to be initialized.
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
		/// Invoked when the item needs to be saved to the data source in independent mode.
		/// </summary>
		/// 
		/// <remarks>
		/// The base implementation of this method uses a DataAdapter with the item's UpdateCommand
		/// in order to save it's fields to the database. Derived classes can override this method to add additional
		/// checks or operations before and after the save, or to change the saving method altogether.
		/// </remarks>
		protected virtual void OnSave()
		{
			// Cannot update when the table is not managed inside the item
			if (_owner != null)
				throw new InvalidOperationException("Cannot independently save an item that is part of a collection");

			// Rather than just allowing the adapter to skip the row, throw and exception to notify the consumer
			if (this.DataState != DataRowState.Added &&
			this.DataState != DataRowState.Modified &&
			this.DataState != DataRowState.Unchanged
				)
				throw new InvalidOperationException("Cannot save an item with the DataState of " + this.DataState.ToString());

			if (DataManager.ProxyMode)
			{
				ProxyRequestAction action = ProxyClient.Request.AddAction(this, MethodInfo.GetCurrentMethod());
				action.OnComplete = delegate()
				{
					DataTable table = ProxyClient.Result[action].GetData<DataTable>("_table");
					_table = table;
					_row = table.Rows[0];

					// TODO: deal with any field backups and shit
				};
			}
			else
			{
				// Create an adapter
				SqlDataAdapter adapter = DataManager.CreateAdapter(this);

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

				if (ProxyServer.InProgress)
					ProxyServer.Current.Result[ProxyServer.Current.CurrentAction].AddData("_table", _table);
			}
		}

		

		/// <summary>
		/// Invoked when the item needs to be deleted from the data source in independent mode.
		/// </summary>
		/// 
		/// <remarks>
		/// <para>
		/// The base implementation of this method uses a DataAdapter with the item's DeleteCommand
		/// in order to delete the record from the database. After deleting, the item
		/// and it's fields remain fully accessible from memory only and so can be resaved.
		/// </para>
		/// <para>
		/// Derived classes can override this method to add additional
		/// checks or operations before and after the delete, or to change the delete process altogether.
		/// </para>
		/// </remarks>
		protected virtual void OnDelete()
		{
			// Cannot update when the table is not managed inside the item
			if (_owner != null)
				throw new InvalidOperationException("Cannot independently delete an item that is part of a collection." +
					" Use the Remove or RemoveAt methods to delete the item from the collection and then use Save to save the data to the database.");

			if (this.DataState == DataRowState.Deleted ||
				this.DataState == DataRowState.Detached
				)
				throw new InvalidOperationException("Cannot delete an item with the DataState of " + this.DataState.ToString());

			if (DataManager.ProxyMode)
			{
				ProxyClient.Request.AddAction(this, MethodInfo.GetCurrentMethod()).OnComplete = delegate()
				{
					_row.SetAdded();
				};
			}
			else
			{
				// Preserve the items so we can re-insert them (deleting clears the row)
				object[] dataBeforeDelete = new object[_row.ItemArray.Length];
				_row.ItemArray.CopyTo(dataBeforeDelete, 0);
				
				// Delete the row
				_row.Delete();

				// Create an adapter
				SqlDataAdapter adapter = DataManager.CreateAdapter(this);

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

				// Re-add the row so it can be accessed again (only when not part of a collection)
				if (_owner == null)
				{
					_table.Rows.Add(_row);
					_row.ItemArray = dataBeforeDelete;
				}
			}
		}

		/// <summary>
		/// Checks if a specified field name is currently available.
		/// </summary>
		/// <param name="fieldName"></param>
		/// <returns></returns>
		public bool IsFieldMissing(string fieldName)
		{
			return _missingFields != null ?
				_missingFields.IndexOf(fieldName) > -1 :
				InnerTable.Columns.IndexOf(fieldName) > -1;
		}

		/// <summary>
		/// Gets the value of the specified field, fetching it from the data source if necessary.
		/// </summary>
		/// <param name="fieldName">The column name of the field to retrieve.</param>
		/// <returns>The field's value.</returns>
		public object GetField(string fieldName)
		{
			return this.GetField(fieldName, true);
		}

		
		/// <summary>
		/// Gets the value of the specified field.
		/// </summary>
		/// 
		/// <param name="fieldName">
		/// The column name of the field to retrieve.
		/// </param>
		/// <param name="retrieveMissing">
		/// When the requested field is missing, fetches the field value from the data source (true),
		/// or returns null (false).
		/// </param>
		/// 
		/// <returns>
		/// The field's value.
		/// </returns>
		protected internal object GetField(string fieldName, bool retrieveMissing)
		{
			// If this field's data is missing, retrieve all missing fields now
			if (!DataManager.ProxyMode && DataManager.AutoRetrieveMissingFields && retrieveMissing && _missingFields != null && _missingFields.Contains(fieldName))
				RetrieveMissingFields();

			DataRow rowToUse = null;
			DataRowVersion versionToUse = DataRowVersion.Default;

			// Check where the requested field is defined
			if (_row.Table.Columns.IndexOf(fieldName) < 0)
			{
				// It's not in the main row, check in the expanded row
				if (_retrievedFieldRow != null && _retrievedFieldRow.Table.Columns.IndexOf(fieldName) >= 0)
					rowToUse = _retrievedFieldRow;
				else
					throw new IndexOutOfRangeException("Specified field " + fieldName + " does not exist in the data item's structure");
			}
			else
			{
				// It's in the main row, so use it
				rowToUse = _row;
			}

			object val;
			
			if (rowToUse == _row && (_row.RowState == DataRowState.Deleted || _row.RowState == DataRowState.Detached))
			{
				// Shouldn't happen
				if (_backupFieldValues == null)
					throw new InvalidOperationException("The item's fields are not available because it has been deleted from- or not yet added to it's parent collection");

				// When the row's fields are unavailable use the backup hashtable
				val = _backupFieldValues[fieldName];
			}
			else
			{
				val = rowToUse[fieldName, versionToUse];
			}

			return (val == DBNull.Value || val == null) ? null : val;
		}

		
		/// <summary>
		/// Sets the value of the specified field.
		/// </summary>
		/// 
		/// <param name="fieldName">
		/// The column name of the field to retrieve.
		/// </param>
		/// <param name="value">
		/// The value to apply to the field.
		/// </param>
		/// 
		/// <remarks>
		/// <para>
		/// Setting the value of a missing field removes the field from the missing field list because it
		/// overrides any current data in the source. An exception is thrown if the field being set is a missing
		/// field that was retrieved after the item was created in collection-owned mode (because there is no way
		/// to save that field - the collection doesn't interact with it). (Problem with his concept - see below)
		/// </para>
		/// <para>
		/// <b>WWNYC:</b> additional code should be added to this method that checks whether the field
		/// represents part of the global definition of the item or only additional information concerning the reference.
		/// If the field represents part of the global definition then it should be read-only regardless of whether
		/// it was retrieved with the rest of the collection or not.
		/// </para>
		/// </remarks>
		//protected internal void SetField(string fieldName, object value)
		public void SetField(string fieldName, object value, string notifyPropertyName)
		{
			if (_row.Table.Columns.IndexOf(fieldName) < 0)
			{
				if (_owner != null)
					throw new InvalidOperationException("Specified field " + fieldName + " cannot be set because it is not a direct part of the collection");
				else
					throw new IndexOutOfRangeException("Specified field " + fieldName + " does not exist in the data item's structure");
			}

			if (_row.RowState == DataRowState.Deleted || _row.RowState == DataRowState.Detached)
			{
				// Update the hashtable when the row is unavailable
				if (_backupFieldValues == null)
					_backupFieldValues = new Hashtable(_row.Table.Columns.Count);

				_backupFieldValues[fieldName] = value == null ? DBNull.Value : value;
			}
			else
				_row[fieldName] = value == null ? DBNull.Value : value;

			if (_missingFields != null)
				_missingFields.Remove(fieldName);

			if (notifyPropertyName != null)
				OnPropertyChanged(notifyPropertyName);
		}

		public void SetField(string fieldName, object value)
		{
			SetField(fieldName, value, fieldName);
		}
		
		/// <summary>
		/// Runs the stand-alone SELECT command to retrieve fields that were not present when the item was
		/// created (in either independent or collection-owned mode).
		/// </summary>
		public virtual void RetrieveMissingFields()
		{
			if (DataManager.ProxyMode)
			{
				ProxyRequestAction action = ProxyClient.Request.AddAction(this, MethodInfo.GetCurrentMethod());
				action.OnComplete = delegate()
				{
					DataTable table = ProxyClient.Result[action].GetData<DataTable>("_table");
					_table = table;
					_missingFields.Clear();

					// When no owner just swap tables, 
					if (_owner == null)
						_row = table.Rows[0];
					else
						_retrievedFieldRow = table.Rows[0];
				};
			}
			else
			{
				if (SelectCommand == null)
					throw new InvalidOperationException("There is no defined SELECT command so missing fields cannot be retrieved.");

				// Only do something if there are missing fields to complete
				if (_missingFields.Count > 0 && _selectCmd != null)
				{
					ConnectionKey key = null;

					// Gets parameter values
					foreach (SqlParameter param in SelectCommand.Parameters)
					{
						if (param.SourceColumn != null && param.SourceColumn != String.Empty)
						{
							// Map to the row or to the backup hash according to state
							param.Value = _row.RowState == DataRowState.Deleted || _row.RowState == DataRowState.Detached ?
							_backupFieldValues[param.SourceColumn] : 
							_row[param.SourceColumn];
						}
						else
							param.Value = DBNull.Value;
					}

					try
					{
						key = DataManager.Current.OpenConnection(this);
						SqlDataReader reader = SelectCommand.ExecuteReader();
						using (reader)
						{
							if (!reader.HasRows)
							{
								throw new DataItemInconsistencyException("Error retrieving item data - item no longer exists");
							}
							else
							{
								// Read the first row
								reader.Read();

								DataRow rowToUse;

								// When object is part of the collection, initialize the internal table to hold retrieved fields
								if (_owner != null && _table == null)
								{
									_table = new DataTable("_table");
									OnInitializeTable();
									_retrievedFieldRow = _table.NewRow();
									_table.Rows.Add(_retrievedFieldRow);

									rowToUse = _retrievedFieldRow;
								}
								else
									rowToUse = _row;

								foreach (string missingField in _missingFields)
								{
									try
									{
										if ((_row.RowState == DataRowState.Deleted || _row.RowState == DataRowState.Detached)
										&& rowToUse == _row)
										{
											// Update the backup hash when state is inaccessible
											_backupFieldValues[missingField] = reader[missingField];
										}
										else
											rowToUse[missingField] = reader[missingField];
									}
									catch (Exception ex)
									{
										// If a field assigment failed, the select command
										throw new DataItemInconsistencyException("Error setting item data - retrieved data does not match item's internal structure", ex);
									}
								}

								// Done getting the missing fields, so clear them
								_missingFields.Clear();
							}
						}
					}
					finally
					{
						DataManager.Current.CloseConnection(key);
					}
				}
				if (ProxyServer.InProgress)
					ProxyServer.Current.Result[ProxyServer.Current.CurrentAction].AddData("_table", _table);
			}
		}

		
		/// <summary>
		/// Backs up the current row values into a hashtable before deleting or detaching the row.
		/// </summary>
		internal void BackupFieldValues()
		{
			if (_owner == null)
				return;

			if (_backupFieldValues == null)
			{
				_backupFieldValues = new Hashtable(_row.Table.Columns.Count);
			}

			foreach(DataColumn column in _row.Table.Columns)
			{
				_backupFieldValues[column.ColumnName] = _row[column];
			}
		}

		
		/// <summary>
		/// Restores the values from the backup hashtable.
		/// </summary>
		protected internal void RestoreFieldValues()
		{
			if (_row.RowState == DataRowState.Deleted || _row.RowState == DataRowState.Detached)
				throw new InvalidOperationException("Cannot restore field values when the item is not in it's parent collection");

			// Don't do anything if nothing to restore
			if (_backupFieldValues == null)
				return;
			
			foreach(DictionaryEntry field in _backupFieldValues)
			{
				_row[field.Key.ToString()] = field.Value;
			}
		}


		internal void SetParentCollection(DataItemCollection owner)
		{
			if (owner == null)
				throw new ArgumentNullException("owner");

			// Don't allow if item is already in collection-owned mode
			if (_owner != null)
				throw new InvalidOperationException("Cannot change the parent collection of an item that already belongs to a different collection.");

			// Import our own row as an external row specifying an owner
			ImportIntoOwner(this.Row, owner);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="owner"></param>
		/// <param name="externalRow"></param>
		private void ImportIntoOwner(DataRow externalRow, DataItemCollection owner)
		{
			// If we have an owner and we're exporting an external row, set our row as a new row of the owner's table
			if (owner != null)
			{
				_owner = owner;
				_row = _owner.InnerTable.NewRow();
			}

			// Set matching fields, check against the stand-alone item's fields
			for (int i = 0; i < this.InnerTable.Columns.Count; i++)
			{
				// Get the name of the field
				string fieldName = this.InnerTable.Columns[i].ColumnName;

				// Get the index of the field for this item's row, can be different from the stand-alone version we're checking against
				int currentRowIndex = _row.Table != this.InnerTable ? _row.Table.Columns.IndexOf(fieldName) : i;

				// Get the index of the field in the external row
				int externalRowIndex = externalRow.Table.Columns.IndexOf(fieldName);

				if (currentRowIndex > -1 && externalRowIndex > -1)
				{
					// Apply the data to the collection row only if both the internal row and the external row have field
					_row[currentRowIndex] = externalRow[externalRowIndex];
				}
				else if (externalRowIndex > -1)
				{
					// Apply the data to the retrieved field row since we already have it
					if (_retrievedFieldRow == null)
					{
						// Create it if it's missing
						_retrievedFieldRow = this.InnerTable.NewRow();
						this.InnerTable.Rows.Add(_retrievedFieldRow);
					}
					_retrievedFieldRow[i] = externalRow[externalRowIndex];
				}
				else
				{
					// Otherwise mark it is as missing for RetrieveMissing to collect later on if necessary
					_missingFields.Add(fieldName);
				}
			}

			// This is a new addition June 3 2007
			if (owner != null)
				BackupFieldValues();
		}

		protected void OnPropertyChanged(string propertyName)
		{
			if (PropertyChanged != null && _notifyOnPropertyChanged)
				PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
		}

		/*=========================*/
		#endregion

		#region Public Properties
		/*=========================*/
		
		/// <summary>
		/// Gets the current state of the item in relation to it's parent collection.
		/// </summary>
		public DataRowState DataState
		{
			get
			{
				return _row.RowState;
			}
		}

		
		/// <summary>
		/// Gets the collection that owns the item. Null if the item is in independent mode.
		/// </summary>
		public DataItemCollection ParentCollection
		{
			get
			{
				return _owner;
			}
		}

		/// <summary>
		/// Gets or sets a value indicating whether to raise a PropertyChanged event when a property changes.
		/// </summary>
		public bool NotifyOnPropertyChanged
		{
			get { return _notifyOnPropertyChanged; }
			set { _notifyOnPropertyChanged = value; }
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
		/// Saves the item to the data source.
		/// </summary>
		public void Save()
		{
			this.OnSave();
		}


		/// <summary>
		/// Deletes the item from the data source.
		/// </summary>
		/// 
		/// <remarks>
		/// This action takes effect right away (no need to call Save after it).
		/// </remarks>
		public void Delete()
		{
			this.OnDelete();
		}

		/// <summary>
		/// Gets a copy of the item's internal data structure. 
		/// </summary>
		public DataTable InnerTableCopy()
		{
			if (_owner == null)
			{
				return this._table.Copy();
			}
			else
			{
				DataTable copy = _owner.InnerTable.Clone();
				copy.ImportRow(this.Row);
				return copy;
			}
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

		#region INotifyPropertyChanged Members

		public event PropertyChangedEventHandler PropertyChanged;

		#endregion

		#region Serialization

		
		protected DataItem(SerializationInfo info, StreamingContext context)
		{
			DataTable table = (DataTable) info.GetValue("_table", typeof(DataTable));

			if (table == null)
				throw new SerializationException("Failed to deserialize DataItem; a deserialized table was not found.");

			_table = table;
			_row = table.Rows[0];

			_actionIndex = (int) info.GetValue("_actionIndex", typeof(int));
			_backupFieldValues = (Hashtable) info.GetValue("_backupFieldValues", typeof(Hashtable));
			_missingFields = (ArrayList) info.GetValue("_missingFields", typeof(ArrayList));

			InitCommands();
		}

		public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			if (_owner != null)
				throw new SerializationException("Cannot directly serialize a DataItem that is part of a collection.");

			info.AddValue("_actionIndex", _actionIndex);
			info.AddValue("_table", _table);
			info.AddValue("_backupFieldValues", _backupFieldValues);
			info.AddValue("_missingFields", _missingFields);
		}
		

		/*
		[OnSerializing]
		private void OnSerializing(StreamingContext context)
		{
			if (_owner != null)
				throw new SerializationException("Cannot directly serialize a DataItem that is part of a collection.");
		}

		[OnDeserialized]
		private void OnDeserialized(StreamingContext context)
		{
			_row = _table.Rows[0];
		}
		*/

		#endregion
	}
}
