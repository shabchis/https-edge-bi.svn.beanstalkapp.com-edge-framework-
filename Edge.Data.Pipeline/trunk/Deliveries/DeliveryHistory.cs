using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Pipeline.Deliveries
{
	public class DeliveryHistory<OperationT>: IList<DeliveryHistoryEntry<OperationT>> where OperationT: struct
	{
		const string ERROR_MSG = "History can only be added (insert/remove/swap/clear not supported).";

		List<DeliveryHistoryEntry<OperationT>> _list;

		private List<DeliveryHistoryEntry<OperationT>> InnerList
		{
			get { return _list ?? (_list = new List<DeliveryHistoryEntry<OperationT>>()); }
		}

		public void Add(OperationT operation, long instanceID)
		{
			InnerList.Add(new DeliveryHistoryEntry<OperationT>(operation, instanceID));
		}

		#region IList<DeliveryHistoryEntry<OperationT>> Members
		//===================================
		int IList<DeliveryHistoryEntry<OperationT>>.IndexOf(DeliveryHistoryEntry<OperationT> item)
		{
			return InnerList.IndexOf(item);
		}

		void IList<DeliveryHistoryEntry<OperationT>>.Insert(int index, DeliveryHistoryEntry<OperationT> item)
		{
			throw new NotSupportedException();
		}

		void IList<DeliveryHistoryEntry<OperationT>>.RemoveAt(int index)
		{
			throw new NotSupportedException(ERROR_MSG);
		}

		public DeliveryHistoryEntry<OperationT> this[int index]
		{
			get { return InnerList[index]; }
			set { throw new NotSupportedException(ERROR_MSG); }
		}
		//===================================
		#endregion

		#region ICollection<DeliveryHistoryEntry<OperationT>> Members
		//===================================

		public void Add(DeliveryHistoryEntry<OperationT> item)
		{
			throw new NotImplementedException();
		}

		void ICollection<DeliveryHistoryEntry<OperationT>>.Clear()
		{
			throw new NotSupportedException(ERROR_MSG);
		}

		public bool Contains(DeliveryHistoryEntry<OperationT> item)
		{
			return InnerList.Contains(item);
		}

		public void CopyTo(DeliveryHistoryEntry<OperationT>[] array, int arrayIndex)
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

		public bool Remove(DeliveryHistoryEntry<OperationT> item)
		{
			throw new NotSupportedException(ERROR_MSG); 
		}

		//===================================
		#endregion

		#region IEnumerable<DeliveryHistoryEntry<OperationT>> Members
		//===================================

		public IEnumerator<DeliveryHistoryEntry<OperationT>> GetEnumerator()
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

	public struct DeliveryHistoryEntry<OperationT>
	{
		KeyValuePair<OperationT, long> _pair;

		public DeliveryHistoryEntry(OperationT operation, long instanceID)
		{
			_pair = new KeyValuePair<OperationT, long>(operation, instanceID);
		}

		public OperationT Operation
		{
			get { return _pair.Key; }
		}
		public long InstanceID
		{
			get { return _pair.Value; }
		}
	}
}
