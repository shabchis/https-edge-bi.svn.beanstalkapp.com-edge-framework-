using System;
using System.Collections.Generic;
using System.Linq;
using Edge.Data.Pipeline;

namespace Edge.Data.Pipeline
{
	public interface IDeliveryChild
	{
		string Key { get; }
		Delivery Delivery { get; set;  }
	}


	public class DeliveryChildList<TChild> : ICollection<TChild> where TChild : class, IDeliveryChild
	{
		Delivery _parentDelivery;
		Dictionary<string, TChild> _dict;

		internal DeliveryChildList(Delivery parentDelivery)
		{
			if (parentDelivery == null)
				throw new ArgumentNullException("parentDelivery");
			_parentDelivery = parentDelivery;
		}

		Dictionary<string, TChild> Internal
		{
			get { return _dict ?? (_dict = new Dictionary<string, TChild>()); }
		}

		public void Add(TChild child)
		{
			if (String.IsNullOrWhiteSpace(child.Key))
				throw new ArgumentException("Delivery child object is missing a key.");

			if (child.Delivery != null)
				throw new InvalidOperationException("Delivery child already belongs to another delivery.");

			child.Delivery = _parentDelivery;
			Internal.Add(child.Key, child);
		}

		public bool Remove(TChild child)
		{
			return Internal.Remove(child.Key);
		}

		public bool Contains(string key)
		{
			return Internal.ContainsKey(key);
		}

		public bool Remove(string key)
		{
			return Internal.Remove(key);
		}

		public TChild this[string key]
		{
			get
			{
				TChild child;
				return Internal.TryGetValue(key, out child) ? child : null;
			}
		}

		#region IEnumerable Members

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return ((IEnumerable<TChild>)this).GetEnumerator();
		}

		#endregion

		#region IEnumerable<TChild> Members

		IEnumerator<TChild> IEnumerable<TChild>.GetEnumerator()
		{
			return Internal.Values.GetEnumerator();
		}

		#endregion

		#region ICollection<TChild> Members

		public void Clear()
		{
			Internal.Clear();
		}

		public bool Contains(TChild item)
		{
			return Internal.ContainsValue(item);
		}

		public void CopyTo(TChild[] array, int arrayIndex)
		{
			Internal.Values.CopyTo(array, arrayIndex);
		}

		public int Count
		{
			get { return Internal.Count; }
		}

		public bool IsReadOnly
		{
			get { return false; }
		}

		#endregion
	}
}
