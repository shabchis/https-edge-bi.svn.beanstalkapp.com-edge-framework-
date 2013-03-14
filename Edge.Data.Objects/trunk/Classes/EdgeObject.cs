using System.Collections.Generic;

namespace Edge.Data.Objects
{
	public partial class EdgeObject : EdgeObjectBase
	{
		public long GK;
		public string TK;
		public Account Account;
		public EdgeType EdgeType;
		
		public Dictionary<EdgeField, object> Fields;

		public override IEnumerable<ObjectDimension> GetObjectDimensions()
		{
			if (Fields == null) yield break;
			foreach (var field in Fields)
			{
				yield return new ObjectDimension {Field = field.Key, Value = field.Value};
			}
		}
	}
	
	public abstract class EdgeObjectBase
	{
		public virtual bool HasChildsObjects
		{
			get { return false; }
		}

		public virtual IEnumerable<ObjectDimension> GetObjectDimensions()
		{
			return null;
		}
	}
}
