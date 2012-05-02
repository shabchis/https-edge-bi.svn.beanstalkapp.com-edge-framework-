using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Objects
{
	public abstract class EdgeObject
	{
		public ulong GK;
		public string Name;
		public string OriginalID;

		public Account Account;

		public ObjectStatus Status;

		public Dictionary<MetaProperty, object> MetaData;
	}

	public class MetaProperty
	{
		public int ID;
		public string Name;
		public Account Account;
		public Channel Channel;
		public Type PropertyType;
	}

	public abstract class ChannelSpecificObject : EdgeObject
	{
		public Channel Channel;
	}

	public enum ObjectStatus
	{
		Unknown = 0,
		Active = 1,
		Paused = 2,
		Suspended = 3,
		Ended = 4,
		Deleted = 5,
		Pending = 6
	}

	public class Account
	{
		public int ID;
		public string Name;
		public Account ParentAccount;
	}

	public class Channel
	{
		public int ID;
		public string Name;
		public ChannelType ChannelType;
	}

	public enum ChannelType
	{
		BackOfficeChannel,
		MarketingChannel
	}

	public abstract class Creative : EdgeObject
	{
	}

	public class TextCreative : Creative
	{
		public TextCreativeType TextType;
		public string Text;
		public string Text2;
	}

	public class ImageCreative : Creative
	{
		public string ImageUrl;
		public string ImageSize;
	}

	public enum TextCreativeType
	{
		Title = 1,
		Body = 2,
		DisplayUrl = 3
	}

	public class Ad : ChannelSpecificObject
	{
		public string DestinationUrl;
		public List<AdCreative> Creatives;
		public List<AdTarget> Targets;
	}

	public class AdCreative : ChannelSpecificObject
	{
		public Ad ParentAd;
		public Creative Creative;
	}

	public class AdTarget : ChannelSpecificObject
	{
		public Ad ParentAd;
		public Target Target;
	}

	public class LandingPage: EdgeObject
	{
		public LandingPageType LandingPageType;
	}

	public enum LandingPageType
	{
		Static,
		Dynamic
	}

	public class SearchQuery : ChannelSpecificObject
	{
		public KeywordTarget Keyword;
	}

	public abstract class Target: EdgeObject
	{
		public string DestinationUrl;
	}

	public class KeywordTarget: Target
	{
		public string Value;
		public KeywordMatchType MatchType;
	}

	public class SearchQuery : EdgeObject
	{
		public KeywordTarget Keyword;
	}

	public enum KeywordMatchType
	{
		Unidentified = 0,
		Broad = 1,
		Phrase = 2,
		Exact = 3
	};

	public class PlacementTarget : Target
	{
		public string Value;
		public PlacementType PlacementType;
	}

	public enum PlacementType
	{
		Unidentified = 0,
		Automatic = 4,
		Managed = 5
	}

	public class GenderTarget : Target
	{
		public Gender Gender;
	}

	public enum Gender
	{
		Unspecified = 0,
		Male = 1,
		Female = 2
	}

	public class AgeGroupTarget : Target
	{
		public int FromAge;
		public int ToAge;
	}

	public class Campaign : ChannelSpecificObject
	{
		public double Budget;
	}

	public class AdGroup : ChannelSpecificObject
	{
		public Campaign ParentCampaign;
	}

	public class CustomValue : ChannelSpecificObject
	{
		public MetaProperty ParentProperty;
		public string Value;
	}
}
