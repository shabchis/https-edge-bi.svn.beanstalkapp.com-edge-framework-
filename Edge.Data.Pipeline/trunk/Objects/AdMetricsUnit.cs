using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Data.Pipeline;

namespace Edge.Data.Objects
{
	public class AdMetricsUnit
	{
		public readonly Guid Guid = Guid.NewGuid();

		public DateTime PeriodStart;
		public DateTime PeriodEnd;
		
		public Ad Ad; //table
		public List<Target> TargetMatches; //table
		public Tracker Tracker;

		public Currency Currency;

		public Dictionary<Measure, double> Measures=new Dictionary<Measure,double>();

		// Values
		public double Cost
		{
			get { return GetMeasure(Measure.Cost); }
			set { SetMeasure(Measure.Cost, value); }
		}

		public long Impressions
		{
			get { return (long) GetMeasure(Measure.Impressions); }
			set { SetMeasure(Measure.Impressions, value); }
		}

		public long UniqueImpressions
		{
			get { return (long)GetMeasure(Measure.UniqueImpressions); }
			set { SetMeasure(Measure.UniqueImpressions, value); }
		}

		public long Clicks
		{
			get { return (long)GetMeasure(Measure.Clicks); }
			set { SetMeasure(Measure.Clicks, value); }
		}

		public long UniqueClicks
		{
			get { return (long)GetMeasure(Measure.UniqueClicks); }
			set { SetMeasure(Measure.UniqueClicks, value); }
		}

		public double AveragePosition
		{
			get { return GetMeasure(Measure.AveragePosition); }
			set { SetMeasure(Measure.AveragePosition, value); }
		}

		double GetMeasure(Measure m)
		{
			double val;
			if (!Measures.TryGetValue(m, out val))
				return 0;
			return val;
		}

		void SetMeasure(Measure m, double value)
		{
			Measures[m] = value;
		}
	}
}
