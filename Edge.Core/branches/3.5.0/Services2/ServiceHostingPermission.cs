using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security;

namespace Edge.Core.Services2
{
	public class ServiceHostingPermission: CodeAccessPermission
	{
		public override IPermission Copy()
		{
			throw new NotImplementedException();
		}

		public override void FromXml(SecurityElement elem)
		{
			throw new NotImplementedException();
		}

		public override IPermission Intersect(IPermission target)
		{
			throw new NotImplementedException();
		}

		public override bool IsSubsetOf(IPermission target)
		{
			throw new NotImplementedException();
		}

		public override SecurityElement ToXml()
		{
			throw new NotImplementedException();
		}
	}
}
