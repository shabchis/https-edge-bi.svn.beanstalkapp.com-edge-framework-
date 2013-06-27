using System;
using System.Collections.Generic;
using System.Linq;
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
		public EdgeCurrency Currency;

		// contains Ad, Targets and other extra fields
		public Dictionary<EdgeField, object> Dimensions; 
		public Dictionary<Measure, double> MeasureValues;

		public DeliveryOutput Output;

		#region Access Properties for Ad a& Targets
		public Ad Ad
		{
			get
			{
				if (Dimensions != null && GetEdgeField != null)
				{
					var edgeField = GetEdgeField(typeof(Ad).Name);
					return Dimensions[edgeField] as Ad;
				}
				return null;
			}
			set
			{
				if (Dimensions == null) Dimensions = new Dictionary<EdgeField, object>();
				if (GetEdgeField == null)
					throw new ArgumentException("GetEdgeField delegate is not set for metrics unit! Use new MetricsUnit {GetEdgeField = GetEdgeField}");

				var edgeField = GetEdgeField(typeof(Ad).Name);
				if (Dimensions.ContainsKey(edgeField))
				{
					Dimensions[edgeField] = value;
				}
				else
				{
					Dimensions.Add(edgeField, value);
				}
			}
		}
		public Dictionary<TargetField, TargetMatch> TargetDimensions
		{
			get
			{
				if (Dimensions != null)
				{
					return Dimensions.Where(d => d.Key is TargetField && d.Value is TargetMatch).ToDictionary(x => x.Key as TargetField, x => x.Value as TargetMatch);
				}
				return null;
			}
		} 
		#endregion

		// function to get edge field for expicit properties (Ad for example)
		public Func<string, EdgeField> GetEdgeField { get; set; }
		#endregion

		#region Public Methods
		/// <summary>
		/// enumeration of all dimensions in MetricsUnit
		/// </summary>
		/// <returns></returns>
		public IEnumerable<ObjectDimension> GetObjectDimensions()
		{
			if (Account != null) yield return new ObjectDimension { Value = new ConstEdgeField { Name = "AccountID", Value = Account.ID, Type = typeof(int) } };
			if (Channel != null) yield return new ObjectDimension { Value = new ConstEdgeField { Name = "ChannelID", Value = Channel.ID, Type = typeof(int) } };
			if (Output != null)  yield return new ObjectDimension { Value = new ConstEdgeField { Name = "OutputID",  Value =Output.OutputID.ToString("N"), Type = typeof(Guid) } };

			yield return new ObjectDimension { Value = new ConstEdgeField { Name = "TimePeriodStart", Value = TimePeriodStart, Type = typeof(DateTime) } };
			yield return new ObjectDimension { Value = new ConstEdgeField { Name = "TimePeriodEnd", Value = TimePeriodEnd, Type = typeof(DateTime) } };
			if (Currency != null) yield return new ObjectDimension { Value = new ConstEdgeField { Name = "Currency", Value = Currency.Code, Type = typeof(string) } };

			if (Dimensions != null)
			{
				foreach (var obj in Dimensions)
					yield return new ObjectDimension {Field = obj.Key, Value = obj.Value};
			}
		} 
		#endregion
	}
}
