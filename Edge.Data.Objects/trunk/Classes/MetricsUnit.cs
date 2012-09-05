using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Objects
{
	public abstract class MetricsUnit
	{
		public Guid Usid = Guid.NewGuid();

		public DateTime TimePeriodStart;
		public DateTime TimePeriodEnd;

		public Currency Currency;

		public List<TargetMatch> TargetDimensions;
		public Dictionary<Measure, double> MeasureValues;

		public abstract IEnumerable<EdgeObject> GetObjectDimensions();
	}

	public class AdMetricsUnit: MetricsUnit
	{
		public Ad Ad;

		public override IEnumerable<EdgeObject> GetObjectDimensions()
		{
			yield return this.Ad;
			foreach (TargetMatch target in this.TargetDimensions)
				yield return target;
		}
	}

	public class GenericMetricsUnit : MetricsUnit
	{
		public Channel Channel;
		public Account Account;

		public Dictionary<MetaProperty, object> PropertyDimensions;

		public override IEnumerable<EdgeObject> GetObjectDimensions()
		{
			foreach (var prop in this.PropertyDimensions)
				if (prop.Value is EdgeObject)
					yield return (EdgeObject)prop.Value;

			foreach (TargetMatch target in this.TargetDimensions)
				yield return target;
		}
	}
}
