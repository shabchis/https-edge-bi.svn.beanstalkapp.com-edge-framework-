using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Diagnostics;

namespace Edge.Core.Services
{
	public interface ILockable
	{
		bool IsLocked { get; }
		void Lock(object key);
		void Unlock(object key);
	}

	[Serializable]
	public class LockException : Exception
	{
		public LockException() : this("Object cannot be modified because it is locked.") { }
		public LockException(string message) : base(message) { }
		public LockException(string message, Exception inner) : base(message, inner) { }
		protected LockException(
		  System.Runtime.Serialization.SerializationInfo info,
		  System.Runtime.Serialization.StreamingContext context)
			: base(info, context) { }
	}

	[DebuggerNonUserCode]
	public class Padlock:ILockable
	{
		object _key;

		public bool IsLocked
		{
			get { return _key != null; }
		}

		public void Lock(object key)
		{
			if (this.IsLocked)
				throw new LockException("Object is already locked.");

			if (key == null)
				throw new ArgumentNullException("key");

			_key = key;
		}

		public void Unlock(object key)
		{
			if (!this.IsLocked)
				throw new InvalidOperationException("Object is not locked.");

			if (key == null)
				throw new ArgumentNullException("key");

			if (!_key.Equals(key))
				throw new LockException("The key does not fit the lock.");
			else
				_key = null;
		}

		public void Ensure()
		{
			if (this.IsLocked)
				throw new LockException();
		}
	}

	[DebuggerNonUserCode]
	[Serializable]
	public class LockableList<T> : IList<T>, ILockable, ISerializable
	{
		List<T> _inner;
		public Func<int, T, bool> OnValidate;

		#region Ctors
		//=================

		public LockableList()
		{
			_inner = new List<T>();
		}

		public LockableList(int capacity)
		{
			_inner = new List<T>(capacity);
		}

		public LockableList(IEnumerable<T> collection)
		{
			_inner = new List<T>(collection);
		}

		//=================
		#endregion

		#region Locking
		//=================
		
		Padlock _lock = new Padlock();

		public bool IsLocked
		{
			get { return _lock.IsLocked; }
		}

		void ILockable.Lock(object key)
		{
			_lock.Lock(key);
		}

		void ILockable.Unlock(object key)
		{
			_lock.Unlock(key);
		}

		//=================
		#endregion

		#region Read
		//=================

		public int IndexOf(T item)
		{
			return _inner.IndexOf(item);
		}

		public bool Contains(T item)
		{
			return _inner.Contains(item);
		}

		public void CopyTo(T[] array, int arrayIndex)
		{
			_inner.CopyTo(array, arrayIndex);
		}

		public int Count
		{
			get { return _inner.Count; }
		}

		public bool IsReadOnly
		{
			get { return ((ILockable)this).IsLocked; }
		}

		public T this[int index]
		{
			get { return _inner[index]; }
			set
			{
				_lock.Ensure();
				if (this.Validate(index, value))
					_inner[index] = value;
			}
		}

		public List<T>.Enumerator GetEnumerator()
		{
			return _inner.GetEnumerator();
		}

		IEnumerator<T> IEnumerable<T>.GetEnumerator()
		{
			return ((IEnumerable<T>)_inner).GetEnumerator();
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return ((System.Collections.IEnumerable)_inner).GetEnumerator();
		}

		//=================
		#endregion

		#region Write
		//=================

		public void Insert(int index, T item)
		{
			_lock.Ensure();
			if (this.Validate(index, item))
				_inner.Insert(index, item);
		}
		public void RemoveAt(int index)
		{
			_lock.Ensure();
			_inner.RemoveAt(index);
		}

		public void Add(T item)
		{
			_lock.Ensure();
			if (this.Validate(-1, item))
				_inner.Add(item);
		}

		public void Clear()
		{
			_lock.Ensure();
			_inner.Clear();
		}

		public bool Remove(T item)
		{
			_lock.Ensure();
			return _inner.Remove(item);
		}

		//=================
		#endregion

		#region For inheritors
		//=================

		protected virtual bool Validate(int index, T item)
		{
			if (OnValidate != null)
				return OnValidate(index, item);
			else
				return true;
		}

		//=================
		#endregion

		#region Serialization
		//=================
		
		public void  GetObjectData(SerializationInfo info, StreamingContext context)
		{
			info.AddValue("_inner", _inner);

			// Only pass on lock status
			info.AddValue("IsLocked", this.IsLocked);
		}

		private LockableList(SerializationInfo info, StreamingContext context)
		{
			_inner = (List<T>)info.GetValue("_inner", typeof(List<T>));

			// Was locked before serialization? Lock 'em up and throw away the key!
			if (info.GetBoolean("IsLocked"))
				_lock.Lock(new object());
		}

		//=================
		#endregion
	}

	[DebuggerNonUserCode]
	[Serializable]
	public class LockableDictionary<TKey, TValue> : IDictionary<TKey, TValue>, ISerializable, ILockable
	{
		Dictionary<TKey, TValue> _inner;
		public Func<TKey, TValue, bool> OnValidate;

		#region Ctors
		//=================

		public LockableDictionary()
		{
			_inner = new Dictionary<TKey, TValue>();
		}

		public LockableDictionary(IDictionary<TKey, TValue> dictionary)
		{
			_inner = new Dictionary<TKey, TValue>(dictionary);
		}

		public LockableDictionary(IEqualityComparer<TKey> comparer)
		{
			_inner = new Dictionary<TKey, TValue>(comparer);
		}

		public LockableDictionary(int capacity)
		{
			_inner = new Dictionary<TKey, TValue>(capacity);
		}

		public LockableDictionary(IDictionary<TKey, TValue> dictionary, IEqualityComparer<TKey> comparer)
		{
			_inner = new Dictionary<TKey, TValue>(dictionary, comparer);
		}

		public LockableDictionary(int capacity, IEqualityComparer<TKey> comparer)
		{
			_inner = new Dictionary<TKey, TValue>(capacity, comparer);
		}

		//=================
		#endregion

		#region Locking
		//=================
		
		Padlock _lock = new Padlock();

		public bool IsLocked
		{
			get { return _lock.IsLocked; }
		}

		void ILockable.Lock(object key)
		{
			_lock.Lock(key);
		}

		void ILockable.Unlock(object key)
		{
			_lock.Unlock(key);
		}

		//=================
		#endregion

		#region Read
		//=================

		public Dictionary<TKey,TValue>.KeyCollection Keys
		{
			get { return _inner.Keys; }
		}

		public Dictionary<TKey,TValue>.ValueCollection Values
		{
			get { return _inner.Values; }
		}

		public bool ContainsKey(TKey key)
		{
			return _inner.ContainsKey(key);
		}

		public bool TryGetValue(TKey key, out TValue value)
		{
			return _inner.TryGetValue(key, out value);
		}

		public TValue this[TKey key]
		{
			get { return _inner[key]; }
			set
			{
				_lock.Ensure();
				if (Validate(key, value))
					_inner[key] = value;
			}
		}

		public int Count
		{
			get { return _inner.Count; }
		}

		public bool IsReadOnly
		{
			get { return _lock.IsLocked; }
		}

		ICollection<TKey> IDictionary<TKey,TValue>.Keys
		{
			get { return this.Keys; }
		}

		ICollection<TValue> IDictionary<TKey,TValue>.Values
		{
			get { return this.Values; }
		}

		bool ICollection<KeyValuePair<TKey,TValue>>.Contains(KeyValuePair<TKey, TValue> item)
		{
			return ((ICollection<KeyValuePair<TKey,TValue>>)_inner).Contains(item);
		}

		void ICollection<KeyValuePair<TKey,TValue>>.CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
		{
			((ICollection<KeyValuePair<TKey,TValue>>)_inner).CopyTo(array, arrayIndex);
		}

		IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey,TValue>>.GetEnumerator()
		{
			return ((IEnumerable<KeyValuePair<TKey,TValue>>)_inner).GetEnumerator();
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return ((System.Collections.IEnumerable)_inner).GetEnumerator();
		}
	
		//=================
		#endregion
	
		#region Write
		//=================

		public void Add(TKey key, TValue value)
		{
			_lock.Ensure();
			if (Validate(key, value))
				_inner.Add(key, value);
		}

		public bool Remove(TKey key)
		{
			_lock.Ensure();
			return _inner.Remove(key);
		}

		public void Clear()
		{
			_lock.Ensure();
			_inner.Clear();
		}

		void ICollection<KeyValuePair<TKey,TValue>>.Add(KeyValuePair<TKey, TValue> item)
		{
			_lock.Ensure();
			if (Validate(item.Key, item.Value))
				((ICollection<KeyValuePair<TKey,TValue>>)_inner).Add(item);
		}

		bool ICollection<KeyValuePair<TKey,TValue>>.Remove(KeyValuePair<TKey, TValue> item)
		{
			_lock.Ensure();
			return ((ICollection<KeyValuePair<TKey,TValue>>)_inner).Remove(item);
		}

		//=================
		#endregion

		#region For inheritors
		//=================

		protected virtual bool Validate(TKey key, TValue value)
		{
			if (OnValidate != null)
				return OnValidate(key, value);
			else
				return true;
		}

		//=================
		#endregion

		#region Serialization
		//=================
		
		public void  GetObjectData(SerializationInfo info, StreamingContext context)
		{
			info.AddValue("_inner", _inner);

			// Only pass on lock status
			info.AddValue("IsLocked", this.IsLocked);
		}

		private LockableDictionary(SerializationInfo info, StreamingContext context)
		{
			_inner = (Dictionary<TKey, TValue>)info.GetValue("_inner", typeof(Dictionary<TKey, TValue>));

			// Was locked before serialization? Lock 'em up and throw away the key!
			if (info.GetBoolean("IsLocked"))
				_lock.Lock(new object());
		}

		//=================
		#endregion
	}


}
