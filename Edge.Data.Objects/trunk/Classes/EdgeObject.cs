using System.Collections.Generic;

namespace Edge.Data.Objects
{
	public partial class EdgeObject : EdgeObjectBase
	{
		public long GK;
		public string TK;
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
