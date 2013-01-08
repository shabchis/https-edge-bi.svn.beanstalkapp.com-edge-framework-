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

		public abstract IEnumerable<object> GetObjectDimensions();
	}

	public class AdMetricsUnit: MetricsUnit
	{
		public Ad Ad;

		public override IEnumerable<object> GetObjectDimensions()
		{
			yield return Ad;
			yield return new ConstEdgeField { Name = "TimePeriodStart", Value = TimePeriodStart, Type = typeof(DateTime)};
			yield return new ConstEdgeField { Name = "TimePeriodEnd", Value = TimePeriodEnd, Type = typeof(DateTime) };
			if (Currency != null) yield return new ConstEdgeField { Name = Currency.GetType().Name, Value = Currency.Code, Type = typeof(string) };

			if (TargetDimensions != null)
			{
				foreach (var target in TargetDimensions)
					yield return target;
			}
		}
	}

	public class GenericMetricsUnit : MetricsUnit
	{
		public Channel Channel;
		public Account Account;

		public Dictionary<EdgeField, object> PropertyDimensions;

		public override IEnumerable<object> GetObjectDimensions()
		{
			if (Account != null) yield return new ConstEdgeField { Name = Account.GetType().Name, Value = Account.ID, Type = typeof(int)};
			if (Channel != null) yield return new ConstEdgeField { Name = Channel.GetType().Name, Value = Channel.ID, Type = typeof(int) };

			yield return new ConstEdgeField { Name = "TimePeriodStart", Value = TimePeriodStart, Type = typeof(DateTime) };
			yield return new ConstEdgeField { Name = "TimePeriodEnd", Value = TimePeriodEnd, Type = typeof(DateTime) };
			if (Currency != null) yield return new ConstEdgeField { Name = Currency.GetType().Name, Value = Currency.Code, Type = typeof(string) };

			if (PropertyDimensions != null)
			{
				foreach (var prop in PropertyDimensions)
					if (prop.Value is EdgeObject)
						yield return prop.Value;
			}

			if (TargetDimensions != null)
			{
				foreach (var target in TargetDimensions)
					yield return target;
			}
		}
	}
}
