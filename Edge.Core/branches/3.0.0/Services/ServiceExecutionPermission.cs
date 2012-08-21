using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security;
using System.Security.Permissions;

namespace Edge.Core.Services
{
	[Serializable]
	public class ServiceExecutionPermission : CodeAccessPermission, IPermission, IUnrestrictedPermission
	{
		public ServiceExecutionPermissionFlags PermissionFlags { get; private set; }

		public ServiceExecutionPermission(ServiceExecutionPermissionFlags permissionFlags = ServiceExecutionPermissionFlags.All)
		{
			this.PermissionFlags = permissionFlags;
		}

		// TODO: this is a temp system till we have time to deal with security sandboxing the appdomains properly
		public new void Demand()
		{
			if (Service.Current != null)
				throw new SecurityException("The current app domain does not have permission to run services directly.");
		}

		public override IPermission Copy()
		{
			return new ServiceExecutionPermission(this.PermissionFlags);
		}

		public override IPermission Intersect(IPermission target)
		{
			if (target == null)
				return null;

			if (!(target is ServiceExecutionPermission)) throw new ArgumentException("target");
			var t = (ServiceExecutionPermission) target;

			return new ServiceExecutionPermission(t.PermissionFlags & this.PermissionFlags);
		}

		public override IPermission Union(IPermission target)
		{
			if (target == null)
				return this.Copy();

			if (!(target is ServiceExecutionPermission)) throw new ArgumentException("target");
			var t = (ServiceExecutionPermission)target;
			
			return new ServiceExecutionPermission(t.PermissionFlags | this.PermissionFlags);
		}

		public override bool IsSubsetOf(IPermission target)
		{
			if (target == null)
				return true;

			if (target != null && !(target is ServiceExecutionPermission)) throw new ArgumentException("target");
			var t = (ServiceExecutionPermission)target;

			return (int)(t.PermissionFlags & this.PermissionFlags) > 0;
		}
		
		public override void FromXml(SecurityElement elem)
		{
			this.PermissionFlags = (ServiceExecutionPermissionFlags)Int32.Parse(elem.Attribute("permissionFlags"));
		}

		public override SecurityElement ToXml()
		{
			SecurityElement esd = new SecurityElement("IPermission");
			String name = typeof(ServiceExecutionPermission).AssemblyQualifiedName;
			esd.AddAttribute("class", name);
			esd.AddAttribute("version", "1.0");
			esd.AddAttribute("permissionFlags", ((int)this.PermissionFlags).ToString());
			return esd;
		}

		public bool IsUnrestricted()
		{
			return this.PermissionFlags == ServiceExecutionPermissionFlags.All;
		}

		public static readonly ServiceExecutionPermission All = new ServiceExecutionPermission();

	}

	[Flags]
	public enum ServiceExecutionPermissionFlags
	{
		None = 0,
		All = 0xf
	}
}
