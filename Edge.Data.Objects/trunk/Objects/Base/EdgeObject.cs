using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Objects
{
	public abstract partial class EdgeObject
	{
		public ulong GK;
		public string Name;

		public Account Account;

		public Dictionary<MetaProperty, object> MetaProperties;
	}

	public partial class MetaProperty
	{
		public int ID;
		public string PropertyName;
		public Account Account;
		public Channel Channel;
		public Type BaseValueType;
	}

	public abstract partial class ChannelSpecificObject : EdgeObject
	{
		public Channel Channel;
		public string OriginalID;
		public ObjectStatus Status;
	}

	public enum ObjectStatus
	{
		Unknown = 0,
		Active = 1,
		Paused = 2,
		Suspended = 3,
		Ended = 4,
		Deleted = 5,
		Pending = 6,
		Duplicate = 999
	}

	public partial class Account
	{
		public int ID;
		public string Name;
		public Account ParentAccount;
	}

	public partial class Channel
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

	public abstract partial class Creative : EdgeObject
	{
	}

	public partial class CompositeCreative : Creative
	{
		public Dictionary<string, SingleCreative> ChildCreatives;
	}

	public abstract partial class SingleCreative : Creative
	{
	}

	public partial class TextCreative : SingleCreative
	{
		public TextCreativeType TextType;
		public string Text;
	}

	public partial class ImageCreative : SingleCreative
	{
		public string ImageUrl;
		public string ImageSize;
	}

	public enum TextCreativeType
	{
		Text = 1,
		Url = 2
	}

	public partial class Ad : ChannelSpecificObject
	{
		public string DestinationUrl;
		public Creative Creative;
		
	}

	public partial class LandingPage : EdgeObject
	{
		public LandingPageType LandingPageType;
	}

	public enum LandingPageType
	{
		Static,
		Dynamic
	}

	public partial class TargetMatch : EdgeObject
	{
		public Target Target;
		public string DestinationUrl;
	}

	public abstract partial class Target : EdgeObject
	{
	}

	public partial class KeywordTarget : Target
	{
		public string Value;
		public KeywordMatchType MatchType;
	}


	public enum KeywordMatchType
	{
		Unidentified = 0,
		Broad = 1,
		Phrase = 2,
		Exact = 3
	};

	public partial class PlacementTarget : Target
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

	public partial class GenderTarget : Target
	{
		public Gender Gender;
	}

	public enum Gender
	{
		Unspecified = 0,
		Male = 1,
		Female = 2
	}

	public partial class AgeGroupTarget : Target
	{
		public int FromAge;
		public int ToAge;
	}

	public partial class Campaign : ChannelSpecificObject
	{
		public double Budget;
	}

	public partial class AdGroup : ChannelSpecificObject
	{
		public Campaign ParentCampaign;
	}

	public partial class Segment : ChannelSpecificObject
	{
		public MetaProperty MetaProperty;
		
	}
}
