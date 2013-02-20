using System.Collections.Generic;

namespace Edge.Data.Objects
{
	public partial class EdgeObject : EdgeObjectBase
	{
		public long GK;
		public string TK;
		public Account Account;
		public EdgeType EdgeType;
		
		public Dictionary<EdgeField, object> ExtraFields;

		public override IEnumerable<ObjectDimension> GetObjectDimensions()
		{
			if (ExtraFields == null) yield break;
			foreach (var field in ExtraFields)
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
