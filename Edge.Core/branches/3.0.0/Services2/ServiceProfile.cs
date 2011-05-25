using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Core.Services2
{
	[Serializable]
	public class ServiceProfile
	{
		public Guid ID;
		public Dictionary<string, object> Parameters;
		public List<ServiceConfiguration> Services;
	}

}
