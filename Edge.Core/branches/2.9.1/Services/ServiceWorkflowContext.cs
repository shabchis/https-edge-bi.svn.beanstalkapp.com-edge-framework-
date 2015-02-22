using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Core.Services
{
	public class ServiceWorkflowContext: IDictionary<string,string>
	{
		Service _service;
		Dictionary<string, string> _dictionary;

		internal ServiceWorkflowContext(Service service, Dictionary<string, string> sync)
		{
			_service = service;
			_dictionary = sync;
		}

		#region IDictionary<string,string> Members

		public void Add(string key, string value)
		{
			_dictionary.Add(key, value);
			_service.SyncWorkflowContext(set: new KeyValuePair<string, string>[] { new KeyValuePair<string, string>(key, value) });
		}

		public bool ContainsKey(string key)
		{
			return _dictionary.ContainsKey(key);
		}

		public ICollection<string> Keys
		{
			get { return _dictionary.Keys; }
		}

		public bool Remove(string key)
		{
			if (_dictionary.Remove(key))
			{
				_service.SyncWorkflowContext(remove: new string[] { key });
				return true;
			}
			else
				return false;
		}

		public bool TryGetValue(string key, out string value)
		{
			return _dictionary.TryGetValue(key, out value);
		}

		public ICollection<string> Values
		{
			get { return _dictionary.Values; }
		}

		public string this[string key]
		{
			get
			{
				return _dictionary[key];
			}
			set
			{
				_dictionary[key] = value;
				_service.SyncWorkflowContext(set: new KeyValuePair<string, string>[] { new KeyValuePair<string, string>(key, value) });
			}
		}

		#endregion

		#region ICollection<KeyValuePair<string,string>> Members

		void ICollection<KeyValuePair<string, string>>.Add(KeyValuePair<string, string> item)
		{
			((ICollection<KeyValuePair<string, string>>)_dictionary).Add(item);
		}

		public void Clear()
		{
			_service.SyncWorkflowContext(clear: true);
		}

		bool ICollection<KeyValuePair<string, string>>.Contains(KeyValuePair<string, string> item)
		{
			return ((ICollection<KeyValuePair<string, string>>)_dictionary).Contains(item);
		}

		public void CopyTo(KeyValuePair<string, string>[] array, int arrayIndex)
		{
			((ICollection<KeyValuePair<string, string>>)_dictionary).CopyTo(array, arrayIndex);
		}

		public int Count
		{
			get { return _dictionary.Count; }
		}

		public bool IsReadOnly
		{
			get { return ((ICollection<KeyValuePair<string, string>>)_dictionary).IsReadOnly; }
		}

		bool ICollection<KeyValuePair<string, string>>.Remove(KeyValuePair<string, string> item)
		{
			throw new NotImplementedException();
		}

		#endregion

		#region IEnumerable<KeyValuePair<string,string>> Members

		public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
		{
			return _dictionary.GetEnumerator();
		}

		#endregion

		#region IEnumerable Members

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return this.GetEnumerator();
		}

		#endregion
	}
}
