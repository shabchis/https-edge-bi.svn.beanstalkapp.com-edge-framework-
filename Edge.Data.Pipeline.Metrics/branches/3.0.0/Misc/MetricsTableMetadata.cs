using System.Collections.Generic;
using Edge.Data.Objects;

namespace Edge.Data.Pipeline.Metrics.Misc
{
	/// <summary>
	/// Metadata of the metrics table which contains table name and list of fields in table structure
	/// each field can be EdgeField or Measure
	/// </summary>
	public class MetricsTableMetadata
	{
		public string TableName { get; set; }
		public List<FieldMetadata> FieldList { get; set; }

		public MetricsTableMetadata()
		{
			FieldList = new List<FieldMetadata>();
		}
	}

	/// <summary>
	/// Table field metadata, can be EdgeField or Measure
	/// Contains various access properties for easy use (such as ID, Name, etc.)
	/// </summary>
	public struct FieldMetadata
	{
		public object Field { get; set; }

		public EdgeField EdgeField
		{
			get { return Field as EdgeField; }
		}
		public Measure Measure
		{
			get { return Field as Measure; }
		}
		public bool IsMeasure
		{
			get { return Field is Measure; }
		}
		public bool IsEdgeField
		{
			get { return Field is EdgeField; }
		}
		public string FieldName
		{
			get 
			{ 
				return	IsMeasure ? Measure.Name :
						IsEdgeField ? EdgeField.Name :
						string.Empty;
			}
		}
		public int FieldId
		{
			get
			{
				return IsMeasure ? Measure.ID :
						IsEdgeField ? EdgeField.FieldID :
						0;
			}
		}

		//public string FieldName { get; set; }
		//public int FieldId { get; set; }
		//public bool IsMeasure { get; set; }
	}
}
