using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Edge.Core.Services;
using System.Diagnostics;

namespace Edge.Core.Scheduling
{
	/// <summary>
	/// Help class for storing shceduling requests by GUID and by request signature
	/// </summary>
	public class InstanceRequestCollection : ICollection<ServiceInstance>
	{
		#region Members

		readonly Dictionary<Guid, ServiceInstance> _requestsByGuid = new Dictionary<Guid, ServiceInstance>();
		readonly Dictionary<string, ServiceInstance> _requestsBySignature = new Dictionary<string, ServiceInstance>();
		#endregion

		#region Indexes
		public ServiceInstance this[Guid guid]
		{
			get
			{
				return _requestsByGuid[guid];
			}
		}

		public ServiceInstance this[int index]
		{
			get
			{
				return _requestsByGuid.Values.ToList()[index];
			}
		} 
		#endregion

		#region Internal Functions
		internal static string GetSignature(ServiceInstance instance)
		{
			return String.Format("BaseConfigurationID:{0},scope:{1},time:{2}", instance.Configuration.GetBaseConfiguration(ServiceConfigurationLevel.Profile).ConfigurationID, instance.SchedulingInfo.SchedulingScope, instance.SchedulingInfo.RequestedTime);

		}
		
		internal bool ContainsSignature(ServiceInstance requestToCheck)
		{
			if (requestToCheck.SchedulingInfo.SchedulingScope == SchedulingScope.Unplanned)
			{
				return false;
			}
			var singature = GetSignature(requestToCheck);
			return _requestsBySignature.ContainsKey(singature);
		}

		internal IEnumerable<ServiceInstance> RemoveNotActivated()
		{
			foreach (var request in _requestsByGuid.RemoveAll(k => k.Value.SchedulingInfo.SchedulingStatus != SchedulingStatus.Activated))
			{
				_requestsBySignature.Remove(GetSignature(request.Value));
				yield return request.Value;
			}
		}

		internal IOrderedEnumerable<ServiceInstance> GetWithSameConfiguration(ServiceInstance currentRequest)
		{
			var servicesWithSameConfiguration =
							from s in _requestsByGuid.Values
							where
								s.Configuration.GetBaseConfiguration(ServiceConfigurationLevel.Template) == currentRequest.Configuration.GetBaseConfiguration(ServiceConfigurationLevel.Template) &&
								// shirat - including Activated service (cannot be executed concurrently) but not Ended services
								//s.SchedulingInfo.SchedulingStatus != SchedulingStatus.Activated &&
								s.State != ServiceState.Ended &&
								s != currentRequest
							orderby s.SchedulingInfo.ExpectedStartTime ascending
							select s;
			return servicesWithSameConfiguration;
		}
		
		internal IOrderedEnumerable<ServiceInstance> GetWithSameProfile(ServiceInstance currentRequest)
		{
			var servicesWithSameProfile =
							from s in _requestsByGuid.Values
							where
								s.Configuration.GetBaseConfiguration(ServiceConfigurationLevel.Profile) == currentRequest.Configuration.GetBaseConfiguration(ServiceConfigurationLevel.Profile) &&
								// shirat - including Activated service (cannot be executed concurrently) but not Ended services
								//s.SchedulingInfo.SchedulingStatus != SchedulingStatus.Activated &&
								s.State != ServiceState.Ended &&
								s != currentRequest
							orderby s.SchedulingInfo.ExpectedStartTime ascending
							select s;

			return servicesWithSameProfile;
		}

		/// <summary>
		/// Remove all service instances which are ended and should not be scheduled
		/// or request which cannot be scheduled
		/// </summary>
		internal void RemoveNotRelevantRequests()
		{
			var requestKeyList = new List<string>();
			foreach (var request in _requestsBySignature)
			{
				if ((request.Value.State == ServiceState.Ended &&
					request.Value.SchedulingInfo.RequestedTime.Add(request.Value.SchedulingInfo.MaxDeviationAfter) < DateTime.Now) ||
					request.Value.SchedulingInfo.SchedulingStatus == SchedulingStatus.CouldNotBeScheduled)
				{
					Debug.WriteLine(DateTime.Now + String.Format(": Remove from scheduled request: '{0}', max deviation after: {1}", request.Key, request.Value.SchedulingInfo.MaxDeviationAfter));
					requestKeyList.Add(request.Key);
				}
			}
			foreach (var key in requestKeyList)
			{
				var request = _requestsBySignature[key];
				_requestsBySignature.Remove(key);
				_requestsByGuid.Remove(request.InstanceID);
			}

		}
		#endregion

		#region ICollection<SchedulingRequest> Members

		public void Add(ServiceInstance item)
		{
			if (item.SchedulingInfo != null)
			{
				_requestsByGuid.Add(item.InstanceID, item);
				// shirat - ? why unplanned are not inserted to dict by signature?
				//if (item.SchedulingInfo.SchedulingScope != SchedulingScope.Unplanned)
				//{
					_requestsBySignature.Add(GetSignature(item), item);
				//}
			}
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

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		#endregion
	}

	#region Extensions
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
	#endregion
}
