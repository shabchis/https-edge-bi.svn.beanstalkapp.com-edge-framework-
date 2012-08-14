using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Core.Scheduling.Objects;
using Legacy = Edge.Core.Services;

namespace Edge.Core.Scheduling
{
	public class SchedulingRequestCollection : ICollection<SchedulingRequest>
	{
		Dictionary<Guid, SchedulingRequest> _requestsByGuid = new Dictionary<Guid, SchedulingRequest>();
		Dictionary<string, SchedulingRequest> _requestsBySignature = new Dictionary<string, SchedulingRequest>();

		public SchedulingRequest this[Guid guid]
		{
			get
			{
				return _requestsByGuid[guid];
			}
		}
		public bool ContainsSignature(SchedulingRequest requestToCheck)
		{
			if (requestToCheck.Rule.Scope == SchedulingScope.Unplanned)
				return false;

			return _requestsBySignature.ContainsKey(requestToCheck.Signature);
		}
		
		#region ICollection<SchedulingRequest> Members

		public void Add(SchedulingRequest item)
		{
			_requestsByGuid.Add(item.RequestID, item);
			if (item.Rule.Scope != SchedulingScope.Unplanned) //since it unplaned it does not matter , their can be many of the same
				_requestsBySignature.Add(item.Signature, item);
		}

		public void Clear()
		{
			_requestsBySignature.Clear();
			_requestsByGuid.Clear();
		}

		public bool Contains(SchedulingRequest item)
		{
			return _requestsByGuid.ContainsKey(item.RequestID);
		}

		public void CopyTo(SchedulingRequest[] array, int arrayIndex)
		{
			_requestsByGuid.Values.CopyTo(array, arrayIndex);

		}

		public int Count
		{
			get { return _requestsByGuid.Count; }
		}

		public bool IsReadOnly
		{
			get { return false; }
		}

		public bool Remove(SchedulingRequest item)
		{
			_requestsBySignature.Remove(item.Signature);
			_requestsByGuid.Remove(item.RequestID);
			return true;
		}

		#endregion

		#region IEnumerable<SchedulingRequest> Members

		public IEnumerator<SchedulingRequest> GetEnumerator()
		{
			return _requestsBySignature.Values.GetEnumerator();
		}

		#endregion

		#region IEnumerable Members

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return this.GetEnumerator();
		}

		#endregion

		internal IEnumerable<SchedulingRequest> RemoveNotActivated()
		{
			_requestsBySignature.RemoveAll(k => k.Value.SchedulingStatus != SchedulingStatus.Activated);
			foreach (var request in _requestsByGuid.RemoveAll(k => k.Value.SchedulingStatus != SchedulingStatus.Activated))
				yield return request.Value;
		}

		internal IOrderedEnumerable<SchedulingRequest> GetWithSameConfiguration(SchedulingRequest currentRequest)
		{
			var servicesWithSameConfiguration =
							from s in _requestsByGuid.Values
							where
								s.Configuration.Name == currentRequest.Configuration.BaseConfiguration.Name && //should be id but no id yet
								s.SchedulingStatus != SchedulingStatus.Canceled &&
								s.SchedulingStatus != SchedulingStatus.Expired &&
								s.Instance.State != Legacy.ServiceState.Ended
							orderby s.ScheduledStartTime ascending
							select s;
			return servicesWithSameConfiguration;
		}
		internal IOrderedEnumerable<SchedulingRequest> GetWithSameProfile(SchedulingRequest currentRequest)
		{
			var servicesWithSameProfile =
							from s in _requestsByGuid.Values
							where
								s.Configuration.Profile == currentRequest.Configuration.Profile &&
								s.Configuration.Name == currentRequest.Configuration.BaseConfiguration.Name &&
								s.SchedulingStatus != SchedulingStatus.Canceled &&
								s.SchedulingStatus != SchedulingStatus.Expired &&
								s.Instance.State != Legacy.ServiceState.Ended
							orderby s.ScheduledStartTime ascending
							select s;

			return servicesWithSameProfile;
		}

	}

	public static class DictionaryExtensions
	{
		public static IEnumerable<KeyValuePair<TKey, TValue>> RemoveAll<TKey, TValue>(this Dictionary<TKey, TValue> dict,
									 Func<KeyValuePair<TKey, TValue>, bool> condition)
		{
			foreach (var cur in dict.Where(condition).ToList())
			{
				dict.Remove(cur.Key);
				yield return cur;
			}
		}

	}


}
