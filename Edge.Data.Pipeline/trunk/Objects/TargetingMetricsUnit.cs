using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Data.Pipeline;

namespace Edge.Data.Objects
{
	public class TargetingMetricsUnit
	{
		public readonly Guid Usid = Guid.NewGuid();

		public DateTime PeriodStart;
		public DateTime PeriodEnd;
		
		public Currency Currency;
		
		public List<Target> TargetMatches = new List<Target>();

		public Dictionary<Measure, double> MeasureValues=new Dictionary<Measure,double>();

	}
}
