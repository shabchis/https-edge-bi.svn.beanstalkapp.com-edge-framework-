using System;
using System.Data;
using System.Data.Sql;
using System.Data.SqlClient;

namespace Edge.Core.Data
{
	/// <summary>
	/// Interface for data-bound objects that synchornize with a data source.
	/// </summary>
	/// 
	/// <remarks>
	/// This interface is used primarily by the DataManager to perform common operations
	/// on both <see cref="DataItem"/> and <see cref="DataItemCollection"/> objects.
	/// </remarks>
	public interface IDataBoundObject
	{
		/// <summary>
		/// Gets the SQL command used by the object to retrieve it's data from the data source via <see cref="Bind"/>.
		/// </summary>
		SqlCommand SelectCommand {get;}
		
		/// <summary>
		/// Gets the SQL command used by the object to insert newly added data into the data source.
		/// </summary>
		SqlCommand InsertCommand {get;}
		
		/// <summary>
		/// Gets the SQL command used by the object to update it's data in the data source.
		/// </summary>
		SqlCommand UpdateCommand {get;}
		
		/// <summary>
		/// Gets the SQL command used by the object to delete any out-of-date data from the data source.
		/// </summary>
		SqlCommand DeleteCommand {get;}

		/// <summary>
		/// Index of this object in a proxy request.
		/// </summary>
		int ProxyActionIndex { get; }

		/// <summary>
		/// Binds the object to it's data source, performing source-to-object synchronization. Unsaved data
		/// is discarded.
		/// </summary>
		void Bind();
		
		/// <summary>
		/// Saves the object's data to the data source, performing object-to-source synchronization.
		/// </summary>
		void Save();
	}
}
