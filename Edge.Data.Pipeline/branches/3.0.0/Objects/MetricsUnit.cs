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

		public List<EdgeObject> ObjectDimensions;
		public List<TargetMatch> TargetDimensions;
		public Dictionary<Measure, double> MeasureValues;

		public DeliveryOutput Output; 
		#endregion

		#region Public Methods
		/// <summary>
		/// enumeration of all dimentions in MetricsUnit
		/// </summary>
		/// <returns></returns>
		public IEnumerable<object> GetObjectDimensions()
		{
			if (Account != null) yield return new ConstEdgeField { Name = Account.GetType().Name, Value = Account.ID, Type = typeof(int) };
			if (Channel != null) yield return new ConstEdgeField { Name = Channel.GetType().Name, Value = Channel.ID, Type = typeof(int) };

			yield return new ConstEdgeField { Name = "TimePeriodStart", Value = TimePeriodStart, Type = typeof(DateTime) };
			yield return new ConstEdgeField { Name = "TimePeriodEnd", Value = TimePeriodEnd, Type = typeof(DateTime) };
			if (Currency != null) yield return new ConstEdgeField { Name = Currency.GetType().Name, Value = Currency.Code, Type = typeof(string) };

			if (ObjectDimensions != null)
			{
				foreach (var obj in ObjectDimensions)
					yield return obj;
			}

			if (TargetDimensions != null)
			{
				foreach (var target in TargetDimensions)
					yield return target;
			}
		} 
		#endregion
	}
}
