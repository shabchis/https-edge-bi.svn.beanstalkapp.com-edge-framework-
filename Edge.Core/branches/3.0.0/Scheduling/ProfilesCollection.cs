using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Core.Services;

namespace Edge.Core.Scheduling
{
	public class ProfilesCollection : ICollection<ServiceProfile>
	{
		private List<ServiceProfile> _profiles = new List<ServiceProfile>();
		private Dictionary<int, ServiceProfile> _profileByAccountId = new Dictionary<int, ServiceProfile>();

		//public ProfileInfo[] GetProfilesInfo()
		//{
		//    ProfileInfo[] infos = new ProfileInfo[_profiles.Count];
		//    lock (this)
		//    {
		//        for (int i = 0; i < _profiles.Count; i++)
		//        {
		//            ProfileInfo p = new ProfileInfo() { AccountID = int.Parse(_profiles[i].Settings["AccountID"].ToString()), AccountName = _profiles[i].Settings["AccountName"].ToString() };
		//            foreach (var item in _profiles[i].ServiceConfigurations)
		//            {
		//                p.Services.Add(item.Name);
		//            }
		//            infos[i] = p;
		//        }
		//    }
		//    return infos;
		//}

		#region ICollection<Profile> Members


		public ServiceProfile this[int accountID]
		{
			get
			{
				return _profileByAccountId[accountID];
			}
		}
		public void Add(ServiceProfile item)
		{
			_profiles.Add(item);
			_profileByAccountId.Add(int.Parse(item.Parameters["AccountID"].ToString()), item);
		}

		public void Clear()
		{
			_profileByAccountId.Clear();
			_profiles.Clear();
		}

		public bool Contains(ServiceProfile item)
		{
			return _profileByAccountId.ContainsKey(int.Parse(item.Parameters["AccountID"].ToString()));
		}

		public void CopyTo(ServiceProfile[] array, int arrayIndex)
		{
			throw new NotImplementedException();
		}

		public int Count
		{
			get
			{
				return _profiles.Count;
			}
		}

		public bool IsReadOnly
		{
			get
			{
				return false;
			}
		}

		public bool Remove(ServiceProfile item)
		{
			if (_profileByAccountId.ContainsKey(int.Parse(item.Parameters["AccountID"].ToString())))
			{
				_profileByAccountId.Remove(int.Parse(item.Parameters["AccountID"].ToString()));
				_profiles.Remove(item);
			}
			return true;

		}

		#endregion

		#region IEnumerable<Profile> Members

		public IEnumerator<ServiceProfile> GetEnumerator()
		{
			throw new NotImplementedException();
		}

		#endregion

		#region IEnumerable Members

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			throw new NotImplementedException();
		}

		#endregion



		public bool TryGetValue(int accountID, out ServiceProfile profile)
		{
			return _profileByAccountId.TryGetValue(accountID, out profile);
		}
	}
}
