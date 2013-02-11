using System.Collections.Generic;

namespace Edge.Data.Pipeline.Metrics.Misc
{
	public class MetricsTableMetadata
	{
		public List<FieldMetadata> FieldList { get; set; }

		public MetricsTableMetadata()
		{
			FieldList = new List<FieldMetadata>();
		}
	}

	public struct FieldMetadata
	{
		public string FieldName { get; set; }
		public int FieldId { get; set; }
		public bool IsMeasure { get; set; }
	}
}
