﻿using System;
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
		private Dictionary<Guid, ServiceProfile> _profileByProfileID = new Dictionary<Guid, ServiceProfile>();

		

		#region ICollection<Profile> Members


		public ServiceProfile this[int accountID]
		{
			get
			{
				return _profileByAccountId[accountID];
			}
		}
		public ServiceProfile this[Guid profileID]
		{
			get
			{
				return _profileByProfileID[profileID];
			}
		}
		public void Add(ServiceProfile item)
		{
			_profiles.Add(item);
			_profileByAccountId.Add(int.Parse(item.Parameters["AccountID"].ToString()), item);
			_profileByProfileID.Add(item.ProfileID, item);
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
			_profiles.CopyTo(array, arrayIndex);
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
			return this._profiles.GetEnumerator();
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
