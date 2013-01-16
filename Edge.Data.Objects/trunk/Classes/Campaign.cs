namespace Edge.Data.Objects
{
	public partial class Campaign : ChannelSpecificObject
	{
		public double Budget;
		public string Name;

		public override string TK { get {return string.Format("Campaign_Name:{0}_Budget{1}", Name, Budget); }}
	}
}
