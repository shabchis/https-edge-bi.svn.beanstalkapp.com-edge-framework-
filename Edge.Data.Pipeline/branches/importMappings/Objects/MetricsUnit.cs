using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Data.Pipeline;

namespace Edge.Data.Objects
{
	public abstract class MetricsUnit
	{
		public Guid Usid = Guid.NewGuid();

		public DateTime TimePeriodStart;
		public DateTime TimePeriodEnd; 

		public Currency Currency;

		public List<Target> TargetDimensions;
		public Dictionary<Measure, double> MeasureValues;

		public DeliveryOutput Output;
	}

	public class AdMetricsUnit: MetricsUnit
	{
		public Ad Ad;
	}

	public class GenericMetricsUnit : MetricsUnit
	{
		public Channel Channel;
		public Account Account;
		
		public Dictionary<Segment, SegmentObject> SegmentDimensions;
	}
}
