using System;
using System.Collections.Generic;
using Edge.Data.Objects;

namespace Edge.Data.Pipeline.Objects
{
	public class MetricsUnit
	{
		#region Properties
		public DateTime TimePeriodStart;
		public DateTime TimePeriodEnd;

		public Channel Channel;
		public Account Account;
		public Currency Currency;

		// Ad 
		// Targets
		//		-- EdgeType
		// Other
		//		-- Field

		public Ad Ad;
		public Dictionary<TargetField, TargetMatch> TargetDimensions;
		public Dictionary<ExtraField, object> ExtraDimensions;
		public Dictionary<Measure, double> MeasureValues;

		public DeliveryOutput Output; 
		#endregion

		#region Public Methods
		/// <summary>
		/// enumeration of all dimentions in MetricsUnit
		/// </summary>
		/// <returns></returns>
		public IEnumerable<ObjectDimension> GetObjectDimensions()
		{
			if (Account != null) yield return new ObjectDimension { Value = new ConstEdgeField { Name = Account.GetType().Name, Value = Account.ID, Type = typeof(int) } };
			if (Channel != null) yield return new ObjectDimension { Value = new ConstEdgeField { Name = Channel.GetType().Name, Value = Channel.ID, Type = typeof(int) } };

			yield return new ObjectDimension { Value = new ConstEdgeField { Name = "TimePeriodStart", Value = TimePeriodStart, Type = typeof(DateTime) } };
			yield return new ObjectDimension { Value = new ConstEdgeField { Name = "TimePeriodEnd", Value = TimePeriodEnd, Type = typeof(DateTime) } };
			if (Currency != null) yield return new ObjectDimension { Value = new ConstEdgeField { Name = Currency.GetType().Name, Value = Currency.Code, Type = typeof(string) }};

			if (Ad != null) yield return new ObjectDimension {Value = Ad};

			if (ExtraDimensions != null)
			{
				foreach (var obj in ExtraDimensions)
					yield return new ObjectDimension {Field = obj.Key, Value = obj.Value};
			}

			if (TargetDimensions != null)
			{
				foreach (var target in TargetDimensions)
					yield return new ObjectDimension { Field = target.Key, Value = target.Value };
			}
		} 
		#endregion
	}
}
