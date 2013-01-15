using System;
using System.Collections.Generic;

namespace Edge.Data.Objects
{
	public partial class EdgeObject : EdgeObjectBase
	{
		public long GK;
		public Account Account;
		public EdgeType EdgeType;
		
		public Dictionary<ExtraField, object> ExtraFields;

		public string TK { get { return ToString(); } }

		public override string ToString()
		{
			var str = EdgeType.Name;
			foreach (var field in ExtraFields)
			{
				str = String.Format("{0}{1}{2}", str, str.Length > 0 ? "_" : String.Empty, field.Value);
			}
			return str;
		}
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
