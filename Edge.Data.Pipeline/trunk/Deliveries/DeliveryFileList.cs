using System;
using System.Collections.Generic;
using System.Linq;
using Edge.Data.Pipeline;

namespace Edge.Data.Pipeline
{

	public class DeliveryFileList : ICollection<DeliveryFile>
	{
		Delivery _parentDelivery;
		Dictionary<string, DeliveryFile> _dict;

		//internal DeliveryFileList()
		//{
		//}

		internal DeliveryFileList(Delivery parentDelivery)
		{
			_parentDelivery = parentDelivery;
		}

		Dictionary<string, DeliveryFile> Internal
		{
			get { return _dict ?? (_dict = new Dictionary<string, DeliveryFile>()); }
		}

		public void Add(DeliveryFile file)
		{
			if (String.IsNullOrWhiteSpace(file.Name))
				throw new ArgumentException("DeliveryFile.Name must be specified.");

			if (file.Delivery != null)
				throw new InvalidOperationException("Delivery file already belongs to another delivery.");

			file.Delivery = _parentDelivery;
			Internal.Add(file.Name, file);
		}

		public bool Remove(DeliveryFile file)
		{
			return Internal.Remove(file.Name);
		}

		public bool Contains(string name)
		{
			return Internal.ContainsKey(name);
		}

		public bool Remove(string name)
		{
			return Internal.Remove(name);
		}

		public DeliveryFile this[string name]
		{
			get
			{
				DeliveryFile file;
				return Internal.TryGetValue(name, out file) ? file : null;
			}
		}

		#region IEnumerable Members

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return ((IEnumerable<DeliveryFile>)this).GetEnumerator();
		}

		#endregion

		#region IEnumerable<DeliveryFile> Members

		IEnumerator<DeliveryFile> IEnumerable<DeliveryFile>.GetEnumerator()
		{
			return Internal.Values.GetEnumerator();
		}

		#endregion

		#region ICollection<DeliveryFile> Members

		public void Clear()
		{
			Internal.Clear();
		}

		public bool Contains(DeliveryFile item)
		{
			return Internal.ContainsValue(item);
		}

		public void CopyTo(DeliveryFile[] array, int arrayIndex)
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
