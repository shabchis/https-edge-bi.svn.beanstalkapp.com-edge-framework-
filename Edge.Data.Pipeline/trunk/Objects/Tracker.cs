using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Pipeline.Objects
{
	public class Tracker
	{
		Ad _ad;
		List<Target> _targets;

		public Ad Ad
		{
			get
			{
				if (_targets != null && _targets.Count > 0)
					throw new InvalidOperationException("Tracker ad is not available when the tracker has targeting data.");
				return _ad;
			}
			set
			{
				if (_targets != null && _targets.Count > 0)
					throw new InvalidOperationException("Tracker ad is not available when the tracker has targeting data.");

				_ad = value;
			}
		}

		public List<Target> Targets
		{
			get
			{
				if (this.Ad != null)
					throw new InvalidOperationException("Tracker targets are not available when the tracker is associated with an ad.");

				return _targets ?? (_targets = new List<Target>());
			}
		}

		public Tracker(Ad ad)
		{
			Ad = ad;
		}
	}
}
