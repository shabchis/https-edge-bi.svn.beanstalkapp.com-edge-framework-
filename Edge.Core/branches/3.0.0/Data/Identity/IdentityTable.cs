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
	/// Manages the identities.
	/// </summary>
	[Serializable]
	[Obsolete]
	public class IdentityTable: ISerializable
	{
		#region Fields
		/*=========================*/
		readonly string _idColumn;
		readonly string[] _identityColumns;
		readonly DataTable _innerTable;
		readonly Dictionary<Identity, DataRow> _identityIndex = new Dictionary<Identity,DataRow>();

		bool _preloaded = false;
		string _preloadCmdText;
		string _insertCmdText;
		string _updateCmdText;
		string[] _additionalColumns;

		object _lock = new object();

		/*=========================*/
		#endregion

		#region Constructors
		/*=========================*/

		public IdentityTable(Type dataItemType, string idColumn, params string[] identityColumns)
		{
			_idColumn = idColumn;

			_identityColumns = identityColumns;
			_innerTable = new DataTable(dataItemType.Name);

			_innerTable.Columns.Add(_idColumn, typeof(long));
			foreach (string col in identityColumns)
				_innerTable.Columns.Add(col);

		}

		/*=========================*/
		#endregion

		#region Public Properties
		/*=========================*/
		public string DataTableName
		{
			get { return _innerTable.TableName; }
		}

		public string PreloadCommandText
		{
			get { return _preloadCmdText; }
			set
			{
				if (_preloadCmdText != null)
					throw new InvalidOperationException("PreloadCommandText can only be set once.");

				_preloadCmdText = String.Format(value, GetAllColumns(true));
			}
		}

		public string InsertCommandText
		{
			get { return _insertCmdText; }
			set
			{
				if (_insertCmdText != null)
					throw new InvalidOperationException("InsertCommandText can only be set once.");

				_insertCmdText = String.Format(value, GetAllColumns(false));
			}
		}

		public string UpdateCommandText
		{
			get { return _updateCmdText; }
			set
			{
				if (_updateCmdText != null)
					throw new InvalidOperationException("UpdateCommandText can only be set once.");

				_updateCmdText = String.Format(value, GetAllColumns(true));
			}
		}

		/// <summary>
		/// When withID is false, null is passed instead of the ID column name
		/// </summary>
		/// <returns></returns>
		private object[] GetAllColumns(bool withID)
		{
			object[] parms = new string[1 + _identityColumns.Length + (_additionalColumns != null ? _additionalColumns.Length : 0)];
			parms[0] = withID ? _idColumn : null;
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
				// Remove old columns
				if (_additionalColumns != null)
					foreach(string existingColumn in _additionalColumns)
						_innerTable.Columns.Remove(existingColumn);

				// Apply new array
				_additionalColumns = value;

				// Add array values as columns
				foreach (string col in _additionalColumns)
					_innerTable.Columns.Add(col);
			}
		}

		/*=========================*/
		#endregion

		#region Public Methods
		/*=========================*/

		public void Preload()
		{
			if (_preloaded)
				throw new InvalidOperationException("Preload can only be called once.");

			if (_preloadCmdText == null)
				throw new InvalidOperationException("PreloadCommantText must be set before calling Preload.");

			// Create a command and associate parameters to columns
			using (DataManager.Current.OpenConnection())
			{
				SqlCommand preloadCmd = DataManager.CreateCommand(_preloadCmdText);
				SqlDataAdapter adapter = new SqlDataAdapter(preloadCmd);
				adapter.Fill(_innerTable);
			}

			// Added by Yaniv.
			// Build the index
			int duplicates = 0;
			foreach (DataRow row in _innerTable.Rows)
			{
				object[] identityValues = new object[_identityColumns.Length];
				for (int i = 0; i < _identityColumns.Length; i++)
					identityValues[i] = row[_identityColumns[i]].ToString();
					//identityValues[i] = row[_identityColumns[i]];

				try
				{
					_identityIndex.Add(new Identity(identityValues), row);
				}
				catch
				{
					duplicates++;
				}
			}

			if (duplicates > 0)
				Log.Write(String.Format("{0} duplicates were found while pre-loading {1} identities.", duplicates, _innerTable.TableName), LogMessageType.Warning);

			_preloaded = true;
		}

		public long GetID(IdentityManagementOptions options, object[] identityValues, object[] additionalValues)
		{
			if (!_preloaded)
				throw new InvalidOperationException("Preload must be called before any IDs can be retrieved.");

			DataRow row = GetRow(options, identityValues, additionalValues);
			if (row != null)
				return (long) row[_idColumn];
			else
				return -1;
		}

		public DataRow GetRow(IdentityManagementOptions options, object[] identityValues, object[] additionalValues)
		{
			if (!_preloaded)
				throw new InvalidOperationException("Preload must be called before any IDs can be retrieved.");

			// Must be synchronized to avoid adding the same row twice
			DataRow returnRow = null;

			lock (_lock)
			{
				
				Identity identity = new Identity(identityValues);
				bool result = _identityIndex.ContainsKey(identity);
				
				if (_identityIndex.TryGetValue(identity, out returnRow))
				{
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
							SyncRow(returnRow, identityValues, additionalValues);
					}
				}
				else if ((options & IdentityManagementOptions.InsertWhenUndefined) != 0)
				{
					// Nothing found so far, generate a new row with a new ID
					returnRow = SyncRow(null, identityValues, additionalValues);
				}
			}
			

			return returnRow;
		} 

		/// <summary>
		/// Create and add a row with the requested values and ID
		/// </summary>
		private DataRow SyncRow(DataRow existingRow, object[] identityValues, object[] additionalValues)
		{
			bool inserting = existingRow == null;

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
				DataManager.ApplyConnection(cmd);

				if (inserting)
				{
					values[0] = cmd.ExecuteScalar();
				}
				else
				{
					int updated = (int) cmd.ExecuteNonQuery();
					if (updated < 1)
						throw new Exception(String.Format("{0} rows were updated while saving a {1} with GK {2}.",
							updated,
							_innerTable.TableName,
							existingRow[_idColumn]
							));
				}
			}

			// Apply new parameters
			DataRow row = inserting ? _innerTable.NewRow() : existingRow;
			row.ItemArray = values;

			// Add the row to the table and to the index
			if (inserting)
			{
				_identityIndex.Add(new Identity(identityValues), row);
				_innerTable.Rows.Add(row);
			}

			row.AcceptChanges();
			return row;
		}


		/*=========================*/
		#endregion

		#region Internal Methods
		/*=========================*/

		/*=========================*/
		#endregion

		#region Serialization
		/*=========================*/

		/// <summary>
		/// For deserialization
		/// </summary>
		private IdentityTable(SerializationInfo info, StreamingContext context)
		{
			_idColumn = info.GetString("IDColumn");
			_identityColumns = (string[]) info.GetValue("IdentityColumns", typeof(string[]));
			_additionalColumns = (string[]) info.GetValue("AdditionalColumns", typeof(string[]));
			_innerTable = (DataTable) info.GetValue("InnerTable", typeof(DataTable));

			// Build the index
			foreach (DataRow row in _innerTable.Rows)
			{
				object[] identityValues = new object[_identityColumns.Length];
				for(int i = 0; i < _identityColumns.Length; i++)
					identityValues[i] = row[_identityColumns[i]];

				_identityIndex.Add(new Identity(identityValues), row);
			}
		}

		public void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			info.AddValue("IDColumn", _idColumn);
			info.AddValue("IdentityColumns", _identityColumns);
			info.AddValue("AdditionalColumns", _additionalColumns);
			info.AddValue("InnerTable", _innerTable);
		}

		/*=========================*/
		#endregion
	}

	/// <summary>
	/// Identity is used as a key for the table. Provides a hashcode combined.
	/// </summary>
	internal struct Identity
	{
		#region Fields
		/*=========================*/
		
		public readonly object[] Values;

		/*=========================*/
		#endregion

		#region Constructor
		/*=========================*/

		/// <summary>
		/// 
		/// </summary>
		/// <param name="values"></param>
		internal Identity(object[] values)
		{
			if (values == null || values.Length < 1)
				throw new ArgumentException("Values cannot be null or zero length", "values");

			Values = values;
		}

		/*=========================*/
		#endregion

		#region Object overrides
		/*=========================*/

		/// <summary>
		/// 
		/// </summary>
		public override bool Equals(object obj)
		{
			if (Values == null || Values.Length < 1 || !(obj is Identity))
				return base.Equals(obj);

			Identity keyToCompare = (Identity) obj;

			// Segment length must match
			if (keyToCompare.Values == null || keyToCompare.Values.Length != Values.Length)
				return false;

			// Compare the order of the segments and their values
			for(int i = 0; i < Values.Length; i++)
			{
				if (Values[i] == null)
				{
					if (keyToCompare.Values[i] != null)
						return false;
				}
				else if (!Values[i].Equals(keyToCompare.Values[i]))
				{
					return false;
				}
			}

			return true;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		public override int GetHashCode()
		{
			if (Values.Length < 1)
				return 0;

			int hashCode = Values[0].GetHashCode();

			// Combine hashcodes with the remaining segments
			for(int i = 1; i < Values.Length; i++)
			{
				hashCode ^= Values[i].GetHashCode();
			}

			return hashCode;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		public override string ToString()
		{
			string output = "{";
			for(int i = 0; i < Values.Length; i++)
			{
				output += Values[i].ToString();
				if (i < Values.Length - 1)
					output += ", ";
			}
			output += "}";

			return output;
		}

		/*=========================*/
		#endregion
	}

}
