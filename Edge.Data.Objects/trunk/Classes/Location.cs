namespace Edge.Data.Objects
{
	public class Location : EdgeObject
	{
		public string Name { get; set; }
		public LocationType LocationType { get; set; }
	}

	public class LocationType : StringValue {}
}
