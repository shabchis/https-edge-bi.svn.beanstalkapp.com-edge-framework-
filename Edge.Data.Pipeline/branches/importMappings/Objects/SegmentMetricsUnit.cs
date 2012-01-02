using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Data.Pipeline;

namespace Edge.Data.Objects
{
	public class SegmentMetricsUnit
	{
		public readonly Guid Guid = Guid.NewGuid();

		public DateTime PeriodStart;
		public DateTime PeriodEnd;

		public Dictionary<Segment, SegmentValue> Segments = new Dictionary<Segment, SegmentValue>();

		public Dictionary<Measure, double> MeasureValues=new Dictionary<Measure,double>();

	}
}
