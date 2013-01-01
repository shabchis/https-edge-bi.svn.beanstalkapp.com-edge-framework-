using System;
using System.Collections.Generic;
using Edge.Data.Objects;

namespace Edge.Data.Pipeline.Objects
{
	public abstract class MetricsUnit
	{	
		public DateTime TimePeriodStart;
		public DateTime TimePeriodEnd;

		public Currency Currency;

		public List<TargetMatch> TargetDimensions;
		public Dictionary<Measure, double> MeasureValues;

		public DeliveryOutput Output;

		public abstract IEnumerable<EdgeObject> GetObjectDimensions();
	}

	public class AdMetricsUnit: MetricsUnit
	{
		public Ad Ad;

		public override IEnumerable<EdgeObject> GetObjectDimensions()
		{
			yield return Ad;
			foreach (TargetMatch target in TargetDimensions)
				yield return target;
		}
	}

	public class GenericMetricsUnit : MetricsUnit
	{
		public Channel Channel;
		public Account Account;

		public Dictionary<ConnectionDefinition, object> PropertyDimensions;

		public override IEnumerable<EdgeObject> GetObjectDimensions()
		{
			foreach (var prop in PropertyDimensions)
				if (prop.Value is EdgeObject)
					yield return (EdgeObject)prop.Value;

			foreach (TargetMatch target in TargetDimensions)
				yield return target;
		}
	}
}
