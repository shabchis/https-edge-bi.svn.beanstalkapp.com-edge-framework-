using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Pipeline
{
	public enum MatchType
	{
		Unidentified = 0,
		Broad = 1,
		Phrase = 2,
		Exact = 3,
		Content = 4,
		WebSite = 5
	};

	[Flags]
	public enum SegmentAssociationFlags
	{
		Keyword=0x01,
		Creative=0x02,
		Site=0x04,
		Campaign=0x08,
		Adgroup=0x10,
		AdgroupKeyword=0x20,
		AdgroupCreative=0x40,
		AdgroupSite=0x80,
		Gateyway = 0x100
	}

	public enum AdVariation
	{
		Unidentified = 0,
		Text = 1,
		Flash = 2,
		Image = 3
	};

	public enum ReportType
	{
		BackOffice = 0,
		AdwordsContent = 1,
		AdwordsCreative = 2,		
	};

	public enum CampaignStatus
	{
		Unknown = -1,
		Pending = 0,
		Active = 1,
		Paused = 2,
		Suspended = 3,
		Ended = 4,
		Deleted = 5
	}

	public enum TrackerReferenceType
	{
		Creative = 0,
		Keyword = 1,
		Site = 2
	}
}
