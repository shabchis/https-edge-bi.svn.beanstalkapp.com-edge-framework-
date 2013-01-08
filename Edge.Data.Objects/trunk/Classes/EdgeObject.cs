using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Objects
{
	public abstract partial class EdgeObject : EdgeObjectBase
	{
		public long GK;
		public Account Account;
		public EdgeType EdgeType;
		
		public Dictionary<ExtraField, object> ExtraFields;
	}
	
	public abstract class EdgeObjectBase
	{
		public virtual bool HasChildsObjects
		{
			get { return false; }
		}

		public virtual IEnumerable<EdgeObject> GetChildObjects()
		{
			return null;
		}
	}


}
