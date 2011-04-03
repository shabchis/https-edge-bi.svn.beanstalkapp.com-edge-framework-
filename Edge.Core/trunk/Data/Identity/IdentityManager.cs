using System;
using System.Collections.Generic;
using System.Data;
using System.ServiceModel;
using System.Runtime.Serialization;
using System.Data.SqlClient;
using System.Threading;
using System.IO;
using Edge.Core.Utilities;

namespace Edge.Core.Data
{
	/// <summary>
	/// Manages the identities of a specific business object type.
	/// </summary>
	public class IdentityManager
	{
		#region Fields
		/*=========================*/
		readonly string _idColumn;
		readonly string[] _identityColumns;
		readonly Type _identityType;
		string[] _additionalColumns;
		string _selectCmdText;
		string _insertCmdText;
		string _updateCmdText;

		/*=========================*/
		#endregion

		#region Constructors
		/*=========================*/

		public IdentityManager(Type businessObjectType, string idColumn, params string[] identityColumns)
		{
			_idColumn = idColumn;
			_identityType = businessObjectType;
			_identityColumns = (string[]) identityColumns.Clone();
		}

		/*=========================*/
		#endregion

		#region Public Properties
		/*=========================*/
		
		public string SelectCommandText
		{
			get { return _selectCmdText; }
			set
			{
				if (_selectCmdText != null)
					throw new InvalidOperationException("SelectCommandText can only be set once.");

				_selectCmdText = String.Format(value, GetAllColumns(true, true));
			}
		}

		public string InsertCommandText
		{
			get { return _insertCmdText; }
			set
			{
				if (_insertCmdText != null)
					throw new InvalidOperationException("InsertCommandText can only be set once.");

				_insertCmdText = String.Format(value, GetAllColumns(false, true));
			}
		}

		public string UpdateCommandText
		{
			get { return _updateCmdText; }
			set
			{
				if (_updateCmdText != null)
					throw new InvalidOperationException("UpdateCommandText can only be set once.");

				_updateCmdText = String.Format(value, GetAllColumns(true, false));
			}
		}

		/// <summary>
		/// Retrieves columns for string formatting.
		/// </summary>
		/// <param name="withID">When false, null is passed instead of the ID column name.</param>
		/// <param name="withIdentityColumns">When false, null is passed for each identity column name.</param>
		/// <returns></returns>
		private object[] GetAllColumns(bool withID, bool withIdentityColumns)
		{
			object[] parms = new string[1 + _identityColumns.Length + (_additionalColumns != null ? _additionalColumns.Length : 0)];
			parms[0] = withID ? _idColumn : null;

			if (withIdentityColumns)
				_identityColumns.CopyTo(parms, 1);

			if (_additionalColumns != null)
				_additionalColumns.CopyTo(parms, 1 + _identityColumns.Length);

			return parms;
		}

		public string[] IdentityColumns
		{
			get { return _identityColumns; }
		}

		/// <summary>
		/// Columns required for an insert but that do not specify identity.
		/// </summary>
		public string[] AdditionalColumns
		{
			get
			{
				return _additionalColumns;
			}
			set
			{
				// Apply new array
				_additionalColumns = value;
			}
		}

		/*=========================*/
		#endregion

		#region Methods
		/*=========================*/

		public long GetID(IdentityManagementOptions options, object[] identityValues, object[] additionalValues)
		{
			// Try to get a current version
			DataTable data = new DataTable();
			DataRow returnRow = null;
			SqlDataAdapter adapter = new SqlDataAdapter();
			adapter.SelectCommand = DataManager.CreateCommand(_selectCmdText);
			for(int i = 0; i < _identityColumns.Length; i++)
			{
				string paramName = String.Format("@{0}", i+1);
				if (adapter.SelectCommand.Parameters.Contains(paramName))
					adapter.SelectCommand.Parameters[paramName].Value = identityValues[i];
			}

			using (DataManager.Current.OpenConnection())
			{
				// Serializable ensures key range locking
				DataManager.Current.StartTransaction(IsolationLevel.Serializable);

				DataManager.Current.AssociateCommands(adapter.SelectCommand);
				adapter.Fill(data);

				if (data.Rows.Count > 0)
				{
					// Found a match
					returnRow = data.Rows[0];

					if ((options & IdentityManagementOptions.UpdateWhenExisting) != 0)
					{
						// Check if anything needs modification
						bool modified = false;
						for (int i = 0; i < _additionalColumns.Length && i < additionalValues.Length; i++)
						{
							if (additionalValues[i] != null && !Object.Equals(additionalValues[i], returnRow[_additionalColumns[i]]))
							{
								modified = true;
								break;
							}
						}

						// Row found in the hash, update its values if necessary
						if (modified)
							SyncRow(data, identityValues, additionalValues);
					}
				}
				else if ((options & IdentityManagementOptions.InsertWhenUndefined) != 0)
				{
					// Nothing found so far, generate a new row with a new ID
					returnRow = SyncRow(data, identityValues, additionalValues);
				}

				DataManager.Current.CommitTransaction();
			}

			return Convert.ToInt64(returnRow[_idColumn]);
		} 

		/// <summary>
		/// Create and add a row with the requested values and ID
		/// </summary>
		private DataRow SyncRow(DataTable data, object[] identityValues, object[] additionalValues)
		{
			bool inserting = data.Rows.Count < 1;
			DataRow existingRow = inserting ? null : data.Rows[0];

			// Construct an array of values: ID, identity values, additional values
			object[] values = new object[1 + _identityColumns.Length + (_additionalColumns == null ? 0 : _additionalColumns.Length)];
			values[0] = inserting ? null : existingRow[_idColumn];
			identityValues.CopyTo(values, 1);
			if (_additionalColumns != null && additionalValues != null)
				additionalValues.CopyTo(values, 1 + identityValues.Length);

			// Convert nulls to DBNull
			for (int i = 0; i < values.Length; i++)
			{
				if (values[i] == null)
					values[i] = DBNull.Value;
			}

			// Insert the new row into the DB, getting an ID
			SqlCommand cmd = DataManager.CreateCommand(inserting ? _insertCmdText : _updateCmdText);
			
			// Offset count by 1 because ID (which is first) is ignored during INSERT
			int offset = inserting ? 1 : 0;
			for (int i = offset; i < cmd.Parameters.Count+offset; i++)
				cmd.Parameters[String.Format("@{0}", i)].Value = values[i];

			using (DataManager.Current.OpenConnection())
			{
				// Execute the command and retrieve the new ID
				DataManager.Current.AssociateCommands(cmd);

				if (inserting)
				{
					values[0] = cmd.ExecuteScalar();
				}
				else
				{
					int updated = (int) cmd.ExecuteNonQuery();
					if (updated < 1)
						throw new Exception(String.Format("{0} rows were updated while saving a {1} with the identity key {2}.",
							updated,
							_identityType,
							existingRow[_idColumn]
							));
				}
			}

			// Apply new parameters
			DataRow row = inserting ? data.NewRow() : existingRow;

			if (inserting)
			{
				row[_idColumn] = values[0];
				data.Rows.Add(row);
				row.AcceptChanges();
			}

			return row;
		}


		/*=========================*/
		#endregion
	}

}
