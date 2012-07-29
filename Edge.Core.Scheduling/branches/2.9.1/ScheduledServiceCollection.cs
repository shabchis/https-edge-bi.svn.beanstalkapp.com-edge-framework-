using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Core.Scheduling.Objects;

namespace Edge.Core.Scheduling
{
	public class SchedulingRequestCollection : ICollection<SchedulingRequest>
	{
		Dictionary<Guid, SchedulingRequest> _requestsByGuid=new Dictionary<Guid,SchedulingRequest>();
		Dictionary<string, SchedulingRequest> _requestsByUniqueness=new Dictionary<string,SchedulingRequest>();

		public bool ContainsSimilar(SchedulingRequest requestToCheck)
		{
			return _requestsByUniqueness.ContainsKey(requestToCheck.UniqueKey);
		}
		public void SetDelete(SchedulingRequest request)
		{
			_requestsByUniqueness.Remove(request.UniqueKey);
			_requestsByGuid.Remove(request.RequestID);
		}

		#region ICollection<SchedulingRequest> Members

		public void Add(SchedulingRequest item)
		{
			_requestsByGuid.Add(item.RequestID, item);
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
			get { return _requestsByGuid.Count;}
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
	}

	public class ScheduledServiceCollection : ICollection<ServiceInstance>
	{
		Dictionary<SchedulingRequest, ServiceInstance> _instanceBySchedulingRequest = new Dictionary<SchedulingRequest, ServiceInstance>();
		List<ServiceInstance> _instanceCollection = new List<ServiceInstance>();
		Dictionary<Guid, ServiceInstance> _instanceByGuid = new Dictionary<Guid, ServiceInstance>();

		public ServiceInstance this[Guid guid]
		{
			get
			{
				return _instanceByGuid[guid];
			}
		}
		public ServiceInstance this[SchedulingRequest schedulingRequest]
		{
			get
			{
				return _instanceBySchedulingRequest[schedulingRequest];
			}
		}
		public ServiceInstance this[int index]
		{
			get
			{
				return _instanceCollection[index];
			}
		}

		#region ICollection<ServiceInstance> Members

		public void Add(ServiceInstance serviceInstance)
		{
			if (serviceInstance.SchedulingRequest == null)
				throw new Exception("Debug only, should find solution for this");

			_instanceBySchedulingRequest.Add(serviceInstance.SchedulingRequest, serviceInstance);
			_instanceByGuid.Add(serviceInstance.LegacyInstance.Guid, serviceInstance);
			_instanceCollection.Add(serviceInstance);
		}

		public void Clear()
		{
			_instanceBySchedulingRequest.Clear();
			_instanceCollection.Clear();
			_instanceByGuid.Clear();
		}

		public bool Contains(ServiceInstance serviceInstance)
		{
			return (_instanceByGuid.ContainsKey(serviceInstance.LegacyInstance.Guid));
		}
		public bool ContainsKey(SchedulingRequest schedulingRequest)
		{
			return (_instanceBySchedulingRequest.ContainsKey(schedulingRequest));

		}
		public bool ContainsKey(Guid guid)
		{
			return (_instanceByGuid.ContainsKey(guid));
		}

		public void CopyTo(ServiceInstance[] array, int arrayIndex)
		{
			_instanceCollection.CopyTo(array, arrayIndex);
		}

		public int Count
		{
			get { return _instanceCollection.Count; }
		}

		public bool IsReadOnly
		{
			get { return false; }
		}

		public bool Remove(ServiceInstance serviceInstance)
		{
			bool removed =
				_instanceBySchedulingRequest.Remove(serviceInstance.SchedulingRequest) ||
				_instanceByGuid.Remove(serviceInstance.LegacyInstance.Guid) ||
				_instanceCollection.Remove(serviceInstance);

			return removed;
		}




		#endregion

		#region IEnumerable<ServiceInstance> Members

		public IEnumerator<ServiceInstance> GetEnumerator()
		{
			return (IEnumerator<ServiceInstance>)_instanceCollection.GetEnumerator();
		}

		#endregion

		#region IEnumerable Members

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return (IEnumerator<ServiceInstance>)_instanceCollection.GetEnumerator();
		}

		#endregion


	}


}
