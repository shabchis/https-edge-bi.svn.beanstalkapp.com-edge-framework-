namespace Edge.Data.Objects
{
	/// <summary>
	/// Generic EdgeObject to describe many 2 many relation between two objects
	/// for example: Campaign - Location, Campaign - Language
	/// </summary>
	public class RelationObject : EdgeObject
	{
		public EdgeObject Object1 { get; set; }
		public EdgeObject Object2 { get; set; }

		public bool IsNegative { get; set; }	// negative relation (exclude), for example: NOT Brazil in Campaign
	}
}
