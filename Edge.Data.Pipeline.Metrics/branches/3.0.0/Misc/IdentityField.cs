using System.Collections.Generic;

namespace Edge.Data.Pipeline.Metrics.Misc
{
	public class IdentityField
	{
		public string FieldName { get; set; }
		public string Value { get; set; }
	}
	public class IdentityObject
	{
		public string TK { get; set; }
		public List<IdentityField> ObjectIdentities { get; set; }

		public IdentityObject()
		{
			ObjectIdentities = new List<IdentityField>();
		}
	}
}
