using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Core.Scheduling.Objects;

namespace Edge.Core.Scheduling
{
	public class SchedulingRequestCollection : ICollection<SchedulingRequest>
	{
		Dictionary<Guid, SchedulingRequest> _requestsByGuid;
		Dictionary<string, SchedulingRequest> _requestsByUniqueness;

		public bool ContainsSimilar(SchedulingRequest requestToCheck)
		{
			throw new NotImplementedException();
		}
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
