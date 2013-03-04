using System.Collections.Generic;

namespace Edge.Data.Pipeline.Metrics.Misc
{
	public enum DeliveryObjectStatus
	{
		New,
		Unchanged,
		Modified
	}

	public class FieldValue
	{
		public string FieldName { get; set; }
		public string Value { get; set; }
		public bool IsIdentity { get; set; }
	}

	public class DeliveryEdgeObject
	{
		public string GK { get; set; }
		public string TK { get; set; }
		public DeliveryObjectStatus Status { get; set; }

		public List<FieldValue> FieldList { get; set; }

		public DeliveryEdgeObject()
		{
			FieldList = new List<FieldValue>();
		}
	}
}
