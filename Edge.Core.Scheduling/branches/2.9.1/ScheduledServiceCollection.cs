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
		Dictionary<string, SchedulingRequest> _requestsByUniqueness = new Dictionary<string, SchedulingRequest>();
		Dictionary<string, SchedulingRequest> _recycledRequestsByUniqueness = new Dictionary<string, SchedulingRequest>();

		public SchedulingRequest this[Guid guid]
		{
			get
			{
				return _requestsByGuid[guid];
			}
		}
		public Dictionary<string, SchedulingRequest> RecycledRequestsByUniqueness
		{
			get
			{
				return _recycledRequestsByUniqueness;
			}
		}
		public bool ContainsSimilar(SchedulingRequest requestToCheck)
		{
			if (requestToCheck.Rule.Scope == SchedulingScope.Unplanned)
				return false;

			return _requestsByUniqueness.ContainsKey(requestToCheck.UniqueKey);
		}
		
		#region ICollection<SchedulingRequest> Members

		public void Add(SchedulingRequest item)
		{
			_requestsByGuid.Add(item.RequestID, item);
			if (item.Rule.Scope != SchedulingScope.Unplanned) //since it unplaned it does not matter , their can be many of the same
				_requestsByUniqueness.Add(item.UniqueKey, item);
		}

		public void Clear()
		{
			_requestsByUniqueness.Clear();
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
			_requestsByUniqueness.Remove(item.UniqueKey);
			_requestsByGuid.Remove(item.RequestID);
			return true;
		}

		#endregion

		#region IEnumerable<SchedulingRequest> Members

		public IEnumerator<SchedulingRequest> GetEnumerator()
		{
			return _requestsByUniqueness.Values.GetEnumerator();
		}

		#endregion

		#region IEnumerable Members

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return this.GetEnumerator();
		}

		#endregion

		public void RemovePending()
		{
			foreach (var request in _requestsByGuid.RemoveAll(k => k.Value.Instance.LegacyInstance.State == Legacy.ServiceState.Uninitialized && k.Value.RequestedTime.Add(k.Value.Rule.MaxDeviationAfter) > DateTime.Now))
			{
				if (!request.Value.Activated && request.Value.RequestedTime.Add(request.Value.Rule.MaxDeviationAfter) > DateTime.Now && request.Value.Rule.Scope != SchedulingScope.Unplanned)
					_recycledRequestsByUniqueness.Add(request.Value.UniqueKey, request.Value);

			}
			_requestsByUniqueness.RemoveAll(k => k.Value.Instance.LegacyInstance.State == Legacy.ServiceState.Uninitialized && k.Value.RequestedTime.Add(k.Value.Rule.MaxDeviationAfter) > DateTime.Now);
		}
		public IOrderedEnumerable<SchedulingRequest> GetServicesWithSameConfiguration(SchedulingRequest currentRequest)
		{
			var servicesWithSameConfiguration =
							from s in _requestsByGuid.Values
							where
								s.Configuration.Name == currentRequest.Configuration.BaseConfiguration.Name && //should be id but no id yet
								s.Instance.State != Legacy.ServiceState.Ended &&
								s.Instance.Canceled == false //runnig or not started yet
							orderby s.Instance.ExpectedStartTime ascending
							select s;
			return servicesWithSameConfiguration;
		}
		public IOrderedEnumerable<SchedulingRequest> GetServicesWithSameProfile(SchedulingRequest currentRequest)
		{
			var servicesWithSameProfile =
							from s in _requestsByGuid.Values
							where
								s.Configuration.Profile == currentRequest.Configuration.Profile &&
								s.Configuration.Name == currentRequest.Configuration.BaseConfiguration.Name &&
								s.Instance.LegacyInstance.State != Legacy.ServiceState.Ended &&
								s.Instance.Canceled == false //not deleted
							orderby s.Instance.ExpectedStartTime ascending
							select s;

			return servicesWithSameProfile;
		}

	}

	//public class ScheduledServiceCollection : ICollection<ServiceInstance>
	//{
	//    Dictionary<SchedulingRequest, ServiceInstance> _instanceBySchedulingRequest = new Dictionary<SchedulingRequest, ServiceInstance>();
	//    List<ServiceInstance> _instanceCollection = new List<ServiceInstance>();
	//    Dictionary<Guid, ServiceInstance> _instanceByGuid = new Dictionary<Guid, ServiceInstance>();

	//    public ServiceInstance this[Guid guid]
	//    {
	//        get
	//        {
	//            return _instanceByGuid[guid];
	//        }
	//    }
	//    public ServiceInstance this[SchedulingRequest schedulingRequest]
	//    {
	//        get
	//        {
	//            return _instanceBySchedulingRequest[schedulingRequest];
	//        }
	//    }
	//    public ServiceInstance this[int index]
	//    {
	//        get
	//        {
	//            return _instanceCollection[index];
	//        }
	//    }

	//    #region ICollection<ServiceInstance> Members

	//    public void Add(ServiceInstance serviceInstance)
	//    {
	//        if (serviceInstance.SchedulingRequest == null)
	//            throw new Exception("Debug only, should find solution for this");

	//        _instanceBySchedulingRequest.Add(serviceInstance.SchedulingRequest, serviceInstance);
	//        _instanceByGuid.Add(serviceInstance.LegacyInstance.Guid, serviceInstance);
	//        _instanceCollection.Add(serviceInstance);
	//    }

	//    public void Clear()
	//    {
	//        _instanceBySchedulingRequest.Clear();
	//        _instanceCollection.Clear();
	//        _instanceByGuid.Clear();
	//    }

	//    public bool Contains(ServiceInstance serviceInstance)
	//    {
	//        return (_instanceByGuid.ContainsKey(serviceInstance.LegacyInstance.Guid));
	//    }
	//    public bool ContainsKey(SchedulingRequest schedulingRequest)
	//    {
	//        return (_instanceBySchedulingRequest.ContainsKey(schedulingRequest));

	//    }
	//    public bool ContainsKey(Guid guid)
	//    {
	//        return (_instanceByGuid.ContainsKey(guid));
	//    }

	//    public void CopyTo(ServiceInstance[] array, int arrayIndex)
	//    {
	//        _instanceCollection.CopyTo(array, arrayIndex);
	//    }

	//    public int Count
	//    {
	//        get { return _instanceCollection.Count; }
	//    }

	//    public bool IsReadOnly
	//    {
	//        get { return false; }
	//    }

	//    public bool Remove(ServiceInstance serviceInstance)
	//    {
	//        bool removed =
	//            _instanceBySchedulingRequest.Remove(serviceInstance.SchedulingRequest) ||
	//            _instanceByGuid.Remove(serviceInstance.LegacyInstance.Guid) ||
	//            _instanceCollection.Remove(serviceInstance);

	//        return removed;
	//    }




	//    #endregion

	//    #region IEnumerable<ServiceInstance> Members

	//    public IEnumerator<ServiceInstance> GetEnumerator()
	//    {
	//        return (IEnumerator<ServiceInstance>)_instanceCollection.GetEnumerator();
	//    }

	//    #endregion

	//    #region IEnumerable Members

	//    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
	//    {
	//        return (IEnumerator<ServiceInstance>)_instanceCollection.GetEnumerator();
	//    }

	//    #endregion


	//}
	public static class DictionaryExtensions
	{
		public static IEnumerable<KeyValuePair<TKey, TValue>> RemoveAll<TKey, TValue>(this Dictionary<TKey, TValue> dict,
									 Func<KeyValuePair<TKey, TValue>, bool> condition)
		{
			foreach (var cur in dict.Where(condition).ToList())
			{
				dict.Remove(cur.Key);
				yield  return  cur;
			}
		}

	}


}
