using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Core.Scheduling.Objects;

namespace Edge.Core.Scheduling
{
	public class ProfilesCollection : ICollection<Profile>
	{
		private List<Profile> _profiles = new List<Profile>();
		private Dictionary<int, Profile> _profileByAccountId = new Dictionary<int, Profile>();

		public ProfileInfo[] GetProfilesInfo()
		{
			ProfileInfo[] infos = new ProfileInfo[_profiles.Count];
			lock (this)
			{
				for (int i = 0; i < _profiles.Count; i++)
				{
					ProfileInfo p = new ProfileInfo() { AccountID = int.Parse(_profiles[i].Settings["AccountID"].ToString()), AccountName = _profiles[i].Name };
					foreach (var item in _profiles[i].ServiceConfigurations)
					{
						p.Services.Add(item.Name);
					}
					infos[i] = p;
				}
			}
			return infos;
		}

		#region ICollection<Profile> Members


		public Profile this[int accountID]
		{
			get
			{
				return _profileByAccountId[accountID];
			}
		}
		public void Add(Profile item)
		{
			_profiles.Add(item);
			_profileByAccountId.Add(int.Parse(item.Settings["AccountID"].ToString()), item);
		}

		public void Clear()
		{
			_profileByAccountId.Clear();
			_profiles.Clear();
		}

		public bool Contains(Profile item)
		{
			return _profileByAccountId.ContainsKey(int.Parse(item.Settings["AccountID"].ToString()));
		}

		public void CopyTo(Profile[] array, int arrayIndex)
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

		public bool Remove(Profile item)
		{
			if (_profileByAccountId.ContainsKey(int.Parse(item.Settings["AccountID"].ToString())))
			{
				_profileByAccountId.Remove(int.Parse(item.Settings["AccountID"].ToString()));
				_profiles.Remove(item);
			}
			return true;

		}

		#endregion

		#region IEnumerable<Profile> Members

		public IEnumerator<Profile> GetEnumerator()
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



		internal bool TryGetValue(int accountID, out Profile profile)
		{
			bool exist = false;
			if (_profileByAccountId.ContainsKey(accountID))
			{
				profile = _profileByAccountId[accountID];
				exist = true;
			}
			return exist;
		}
	}
}
