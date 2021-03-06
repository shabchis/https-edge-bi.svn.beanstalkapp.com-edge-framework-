﻿using System;
using System.Data;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using Microsoft.SqlServer.Server;


public partial class StoredProcedures
{
	[Microsoft.SqlServer.Server.SqlProcedure]
	public static void ImportStage1(string tablePrefix)
	{
		const string currentConnection = "context connection=true";

		using (SqlConnection connection = new SqlConnection(currentConnection))
		{
			SqlCommand cmdCampaigns = new SqlCommand(String.Format(@"
				select
					ad.Campaign_Account				as AccountID,
					ad.Campaign_Channel				as ChannelID,
					ad.Campaign_Name				as Name,
					ad.Campaign_OriginalID			as OriginalID,
					ad.Campaign_Status				as Status
					GK_GetCampaignGK
					(
							ad.Campaign_Account,
							ad.Campaign_Channel,
							ad.Campaign_Name,
							ad.Campaign_OriginalID,
							ad.Campaign_Channel
					)								as GK
				into
					#campaigns
				from
					{0}_Ad ad
				group by
					ad.Campaign_Account,
					ad.Campaign_Channel,
					ad.Campaign_Name,
					ad.Campaign_OriginalID,
					ad.Campaign_Status
				;

				-- create primary key for fast lookup
				alter table #campaigns with nocheck
					add constraint pk_campaigns primary key clustered (AccountID, ChannelID, Name) -- TODO: lookup bya
				;
			"));

			SqlCommand cmdAdgroups = new SqlCommand(String.Format(@"
				select
					campaign.GK					as CampaignGK,
					adgroup.Value				as Name,
					adgroup.ValueOriginalID		as OriginalID,
					GK_GetAdgroupGK
					(
						campaign.AccountID,
						campaign.ChannelID,
						campaign.GK,
						adgroup.Value,
						adgroup.ValueOriginalID
					)							as GK
				into
					#adgroups
				from
					{0}_Ad ad
					inner join {0}_AdSegment adgroup on
						adgroup.AdUsid = ad.AdUsid
					inner join #campaigns campaign on
						campaign.AccountID = ad.Campaign_Account and
						campaign.ChannelID = ad.Campaign_Channel and
						campaign.Name = ad.Campaign_Name
				group by
					campaign.AccountID,
					campaign.ChannelID,
					campaign.GK,
					campaign.Name,
					adgroup.Value,
					adgroup.ValueOriginalID
				;

				-- create primary key for fast lookup
				alter table #adgroups with nocheck
					add constraint pk_campaigns primary key clustered (CampaignGK, Name)
				;
			"));

			SqlCommand cmd = new SqlCommand(String.Format(@"
				select
					ad.AdUsid,
					campaign.GK						as Campaign_GK,
					adgroup.GK						as Adgroup_GK,
					creative_title.Field2			as Creative_Title,
					creative_desc.Field2			as Creative_Desc1,
					creative_desc2.Field2			as Creative_Desc2,
					creative_dispUrl.Field2			as Creative_DisplayUrl,
					ad.Name							as AdgroupCreative_Name,
					ad.OriginalID					as AdgroupCreative_OriginalID,
					ad.DestinationUrl				as AdgroupCreative_DestUrl,
					-- TODO: fields for AdTarget? (Facebook)
					
				into
					#creatives
				from
					{0}_Ad ad
					-- TODO: join with #campaigns and #adgroups to get GKs
					left outer join {0}_AdCreative creative_title on
						creative_title.AdUsid = ad.AdUsid and
						creative_title.CreativeType = 1 and					-- (text creative)
						creative_title.Field1 = 1							-- (title)
					left outer join {0}_AdCreative creative_desc on			-- TODO: distinguish between Desc1 and Desc2 on google? limit to 1? concat?
						creative_desc.AdUsid = ad.AdUsid and
						creative_desc.CreativeType = 1 and					-- (text creative)
						creative_desc.Field1 = 2 and							-- (body)
						(creative_desc.Name is null or creative_desc.Name != 'Desc2')
					left outer join {0}_AdCreative creative_desc2 on			-- TODO: distinguish between Desc1 and Desc2 on google? limit to 1? concat?
						creative_desc2.AdUsid = ad.AdUsid and
						creative_desc2.CreativeType = 1 and					-- (text creative)
						creative_desc2.Field1 = 2 and
						(creative_desc2.Name is not null and creative_desc2.Name = 'Desc2')
					left outer join {0}_AdCreative creative_dispUrl on		
						creative_dispUrl.AdUsid = ad.AdUsid and
						creative_dispUrl.CreativeType = 2					-- (URL creative)

				",
				 tablePrefix), connection);

			//using (SqlDataReader reader = connection.ex
		}
	}
};
