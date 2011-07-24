using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Pipeline
{
	public class DeliveryHistory: IList<DeliveryHistoryEntry>
	{
		const string ERROR_MSG = "History can only be added (insert/remove/swap/clear not supported).";

		List<DeliveryHistoryEntry> _list;

		private List<DeliveryHistoryEntry> InnerList
		{
			get { return _list ?? (_list = new List<DeliveryHistoryEntry>()); }
		}

		public void Add(DeliveryOperation operation, long? serviceInstanceID, Dictionary<string,object> parameters = null)
		{
			InnerList.Add(new DeliveryHistoryEntry(operation, serviceInstanceID, parameters));
		}

		#region IList<DeliveryHistoryEntry> Members
		//===================================
		public int IndexOf(DeliveryHistoryEntry item)
		{
			return InnerList.IndexOf(item);
		}

		void IList<DeliveryHistoryEntry>.Insert(int index, DeliveryHistoryEntry item)
		{
			throw new NotSupportedException();
		}

		void IList<DeliveryHistoryEntry>.RemoveAt(int index)
		{
			throw new NotSupportedException(ERROR_MSG);
		}

		public DeliveryHistoryEntry this[int index]
		{
			get { return InnerList[index]; }
			set { throw new NotSupportedException(ERROR_MSG); }
		}
		//===================================
		#endregion

		#region ICollection<DeliveryHistoryEntry> Members
		//===================================

		public void Add(DeliveryHistoryEntry item)
		{
			_list.Add(item);
		}

		void ICollection<DeliveryHistoryEntry>.Clear()
		{
			throw new NotSupportedException(ERROR_MSG);
		}

		public bool Contains(DeliveryHistoryEntry item)
		{
			return InnerList.Contains(item);
		}

		public void CopyTo(DeliveryHistoryEntry[] array, int arrayIndex)
		{
			InnerList.CopyTo(array, arrayIndex);
		}

		public int Count
		{
			get { return InnerList.Count; }
		}

		public bool IsReadOnly
		{
			get { return false; }
		}

		public bool Remove(DeliveryHistoryEntry item)
		{
			throw new NotSupportedException(ERROR_MSG); 
		}

		//===================================
		#endregion

		#region IEnumerable<DeliveryHistoryEntry> Members
		//===================================

		public IEnumerator<DeliveryHistoryEntry> GetEnumerator()
		{
			return InnerList.GetEnumerator();
		}

		#endregion

		#region IEnumerable Members

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return this.GetEnumerator();
		}

		//===================================
		#endregion
	}

	public enum DeliveryOperation
	{
		Initialized = 1,
		Retrieved = 2,
		Imported = 3,
		Committed = 4,
		RolledBack = 5,
		Validated = 6
	}

	public class DeliveryHistoryEntry
	{
		internal DeliveryHistoryEntry(DeliveryOperation operation, long? serviceInstanceID, Dictionary<string,object> parameters = null)
		{
			this.Operation = operation;
			this.ServiceInstanceID = serviceInstanceID;
			this.DateRecorded = DateTime.Now;
			this.Parameters = parameters;
		}

		public DeliveryOperation Operation
		{
			get;
			private set;
		}
		public long? ServiceInstanceID
		{
			get;
			private set;
		}
		public DateTime DateRecorded
		{
			get;
			private set;
		}

		public Dictionary<string, object> Parameters
		{
			get;
			private set;
		}
	}
}
