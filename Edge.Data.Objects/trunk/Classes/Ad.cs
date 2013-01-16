using System;
using System.Collections.Generic;

namespace Edge.Data.Objects
{
	public partial class Ad : ChannelSpecificObject
	{
		public string DestinationUrl;

		public List<TargetDefinition> TargetDefinitions;
		public CreativeDefinition CreativeDefinition;

		public override string TK {get { return String.Format("Ad_{0}", DestinationUrl);} }
	}
}
