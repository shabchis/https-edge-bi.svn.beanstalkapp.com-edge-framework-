using System;
using System.Collections.Generic;
using System.Data;
using System.ServiceModel;
using System.Runtime.Serialization;
using System.Data.SqlClient;
using Edge.Core.Services;
using Edge.Core.Data.Proxy;

namespace Edge.Core.Data
{
	[Flags]
	public enum IdentityManagementOptions
	{
		InsertWhenUndefined = 0x1,
		UpdateWhenExisting = 0x2,
		Default = InsertWhenUndefined | UpdateWhenExisting
	}

	/// <summary>
	/// Service contract for the identity manager.
	/// </summary>
	[ServiceContract]
	[Obsolete]
	public interface IIdentityManager
	{
		[OperationContract]
		[NetDataContract]
		long GetID(Type dataItemType, IdentityManagementOptions options, params object[] values);
	}

	/// <summary>
	/// Base class for an identity management service.
	/// </summary>
	[Obsolete]
	public abstract class IdentityManagerService: Service, IIdentityManager
	{
		Dictionary<Type, IdentityTable> _types = new Dictionary<Type,IdentityTable>();

		/// <summary>
		/// Contains the identity management tables by type.
		/// </summary>
		protected Dictionary<Type, IdentityTable> Tables
		{
			get { return _types; }
		}

		/// <summary>
		/// Retrieves an ID (either existing or new) of the DataItem with the specified identity.
		/// </summary>
		/// <typeparam name="DataItemT">The type of DataItem for which to retrieve an ID.</typeparam>
		/// <param name="values">
		///		The identity values followed by additional values required for new items.
		///		The order of the values must match the order of the columns.
		///	</param>
		/// <returns>The unique ID that matches the identity.</returns>
		public long GetID(Type dataItemType, IdentityManagementOptions options, params object[] values)
		{
			IdentityTable table;
			if (!_types.TryGetValue(dataItemType, out table))
				throw new KeyNotFoundException("The DataItem type {0} does not have an identity management table.");

			if (values.Length < table.IdentityColumns.Length)
				throw new ArgumentException("The number of values must be at least the number of identity columns.", "values"); 

			// Retrieve the identity values from the values array
			object[] identityValues = new object[table.IdentityColumns.Length];
			for(int i = 0; i < table.IdentityColumns.Length; i++)
				identityValues[i] = values[i].ToString();
			//identityValues[i] = values[i]; // Changed by Yaniv

			// Retrieve additional values from the values array (if avaiable)
			object[] additionalValues = null;
			if (table.AdditionalColumns != null)
			{
				additionalValues = new object[table.AdditionalColumns.Length];
				for (int i = 0; i < table.AdditionalColumns.Length; i++)
				{
					// Get the value if specified, otherwise null
					additionalValues[i] = i + table.IdentityColumns.Length < values.Length ?
						values[i + table.IdentityColumns.Length] : 
						null;
				}
			}

			return table.GetID(options, identityValues, additionalValues);
		}

		protected override ServiceOutcome DoWork()
		{
			// Just enter waiting mode
			return ServiceOutcome.Unspecified;
		}
	}

}
