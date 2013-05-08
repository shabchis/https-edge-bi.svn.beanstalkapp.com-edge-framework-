using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Objects
{
	public abstract partial class MetricsUnit
	{	
		public DateTime TimePeriodStart;
		public DateTime TimePeriodEnd;

		public Currency Currency;

		public CreativeMatch CreativeMatch;
		public List<TargetMatch> TargetMatches;
		public Dictionary<ExtraField, EdgeObject> ExtraDimensions; // Not sure about this yet
		
		public Dictionary<Measure, double> MeasureValues;

		public virtual IEnumerable<EdgeObject> GetDimensions()
		{
			throw new NotImplementedException();
			//foreach (TargetMatch match in this.TargetMatches)
			//    yield return match;

			//yield return CreativeMatch;
			//foreach (TargetMatch match in this.TargetMatches)
			//    yield return match;
		}
	}

	public partial class AdMetricsUnit: MetricsUnit
	{
		public Ad Ad;
	}

	public partial class GenericMetricsUnit : MetricsUnit
	{
		public Channel Channel;
		public Account Account;
	}
}
