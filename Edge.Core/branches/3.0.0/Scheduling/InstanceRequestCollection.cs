using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Core.Services;

namespace Edge.Core.Scheduling
{
	public class InstanceRequestCollection : ICollection<ServiceInstance>
	{
		Dictionary<Guid, ServiceInstance> _requestsByGuid = new Dictionary<Guid, ServiceInstance>();
		Dictionary<string, ServiceInstance> _requestsBySignature = new Dictionary<string, ServiceInstance>();

		public ServiceInstance this[Guid guid]
		{
			get
			{
				return _requestsByGuid[guid];
			}
		}
		public string GetSignature(ServiceInstance instance)
		{
			return String.Format("profile:{0},base:{1},name:{2},scope:{3},time:{4}", instance.Configuration.Profile.Parameters["AccountID"], instance.Configuration.BaseConfiguration.ServiceName, instance.Configuration.ServiceName, instance.SchedulingInfo.Scope, instance.SchedulingInfo.RequestedTime);

		}
		public bool ContainsSignature(ServiceInstance requestToCheck)
		{
			if (requestToCheck.SchedulingInfo.Scope == SchedulingScope.Unplanned)
				return false;

			return _requestsBySignature.ContainsKey(GetSignature(requestToCheck));
		}

		#region ICollection<SchedulingRequest> Members

		public void Add(ServiceInstance item)
		{
			_requestsByGuid.Add(item.InstanceID, item);
			if (item.SchedulingInfo.Scope != SchedulingScope.Unplanned) //since it unplaned it does not matter , their can be many of the same
				_requestsBySignature.Add(GetSignature(item), item);
		}

		public void Clear()
		{
			_requestsBySignature.Clear();
			_requestsByGuid.Clear();
		}

		public bool Contains(ServiceInstance item)
		{
			return _requestsByGuid.ContainsKey(item.InstanceID);
		}

		public void CopyTo(ServiceInstance[] array, int arrayIndex)
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

		public bool Remove(ServiceInstance item)
		{
			_requestsBySignature.Remove(GetSignature(item));
			_requestsByGuid.Remove(item.InstanceID);
			return true;
		}

		#endregion

		#region IEnumerable<SchedulingRequest> Members

		public IEnumerator<ServiceInstance> GetEnumerator()
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

		internal IEnumerable<ServiceInstance> RemoveNotActivated()
		{
			_requestsBySignature.RemoveAll(k => k.Value.SchedulingInfo.SchedulingStatus != SchedulingStatus.Activated);
			foreach (var request in _requestsByGuid.RemoveAll(k => k.Value.SchedulingInfo.SchedulingStatus != SchedulingStatus.Activated))
				yield return request.Value;
		}

		internal IOrderedEnumerable<ServiceInstance> GetWithSameConfiguration(ServiceInstance currentRequest)
		{
			var servicesWithSameConfiguration =
							from s in _requestsByGuid.Values
							where
								s.Configuration.ServiceName == currentRequest.Configuration.BaseConfiguration.ServiceName && //should be id but no id yet								
								s.SchedulingInfo.SchedulingStatus != SchedulingStatus.Activated 								
							orderby s.SchedulingInfo.ExpectedStartTime ascending
							select s;
			return servicesWithSameConfiguration;
		}
		internal IOrderedEnumerable<ServiceInstance> GetWithSameProfile(ServiceInstance currentRequest)
		{
			var servicesWithSameProfile =
							from s in _requestsByGuid.Values
							where
								s.Configuration.Profile == currentRequest.Configuration.Profile &&
								s.Configuration.ServiceName == currentRequest.Configuration.BaseConfiguration.ServiceName &&
								s.SchedulingInfo.SchedulingStatus != SchedulingStatus.Activated
							orderby s.SchedulingInfo.ExpectedStartTime ascending
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
