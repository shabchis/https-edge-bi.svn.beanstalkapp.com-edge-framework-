using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Data.Objects.Reflection;
using System.Data.SqlClient;

namespace Edge.Data.Objects
{
	public class Segment
	{
		#region Const
		public static class Common
		{
			public const string Campaign = "Campaign";
			public const string AdGroup = "AdGroup";
            public const string Tracker = "Tracker";
            public const string LandingPage = "LandingPage";
            public const string USP = "USP";
            public const string Region = "Region";
            public const string Language = "Language";
            public const string Theme = "Theme";
		}
		#endregion

		public Account Account {get; set;}
		public Channel Channel {get; set; }

		public int ID { get; set; }
		public string Name { get; set; }

		public static Dictionary<string, Segment> GetSegments(Account account, Channel channel, SqlConnection connection, SegmentOptions options, OptionsOperator @operator = OptionsOperator.And)
		{
			return new Dictionary<string, Segment>()
			{
				{Common.Campaign,  new Segment() { ID = -875, Name = Common.Campaign }},
				{Common.AdGroup, new Segment() { ID = -876, Name = Common.AdGroup }},
				{Common.Tracker, new Segment() { ID = -977, Name=Common.Tracker }},

				{Common.LandingPage, new Segment() { ID = -823, Name=Common.LandingPage }},
				{Common.USP, new Segment() { ID = -824, Name=Common.USP }},
				{Common.Region, new Segment() { ID = -825, Name=Common.Region }},
				{Common.Language, new Segment() { ID = -826, Name=Common.Language }},
				{Common.Theme, new Segment() { ID = -827, Name=Common.Theme }},
			};
		}
	}

	public class SegmentObject : MappedObject
	{
		//public Account Account;
		//public Channel Channel;
		public ObjectStatus Status = ObjectStatus.Unknown;

		public string OriginalID;
		public string Value;

		//public List<Segment> Segments = new List<Segment>();
		public Dictionary<ExtraField, object> ExtraFields = new Dictionary<ExtraField, object>();
	}

	[MappedObjectTypeID(75)]
	public class Campaign : SegmentObject
	{
		/// <summary>
		/// Same as Value.
		/// </summary>
		public string Name
		{
			get { return this.Value; }
			set { this.Value = value; }
		}

		[MappedObjectFieldIndex(1)]
		public double? Budget;
	}

	[MappedObjectTypeID(76)]
	public class AdGroup : SegmentObject
	{
		/// <summary>
		/// Same as Value.
		/// </summary>
		public string Name
		{
			get { return this.Value; }
			set { this.Value = value; }
		}

		[MappedObjectFieldIndex(1, ValueSource="Value")]
		public Campaign Campaign;
	}

	[Flags]
	public enum SegmentOptions
	{
		None = 0x00,
		All = 0xff
	}
}
