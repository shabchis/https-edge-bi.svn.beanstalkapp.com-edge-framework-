using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eggplant.Entities.Persistence;
using Eggplant.Entities.Model;
using Eggplant.Entities.Queries;

namespace Edge.Data.Objects
{
	public partial class Channel
	{
		public static class Mappings
		{
			public static Mapping<Channel> Default = EdgeUtility.EntitySpace.CreateMapping<Channel>(account => account
				.Identity(Channel.Identities.Default)
				.Map<int>(Channel.Properties.ID, "ID")
				.Map<string>(Channel.Properties.Name, "Name")
				.Map<string>(Channel.Properties.DisplayName, "DisplayName")
				.Map<ChannelType>(Channel.Properties.ChannelType, "ChannelType", val => val == null ? ChannelType.Unknown : (ChannelType)val)
			);
		}

		public static class Identities
		{
			public static IdentityDefinition Default = new IdentityDefinition(Channel.Properties.ID);
		}

		public static class Queries
		{
			public static QueryTemplate<Channel> Get = EdgeUtility.EntitySpace.CreateQueryTemplate<Channel>(Mappings.Default)
				.RootSubquery(EdgeUtility.GetSql<Channel>("Get"), init => init
					.PersistenceParam("@channelID", fromQueryParam: "channelID")
				)
				.Input<int>("channelID", required: false, defaultValue: -1)
			;
		}

		public static IEnumerable<Channel> Get(PersistenceConnection connection = null)
		{
			return Queries.Get.Start()
				.Connect(connection)
				.Execute();
		}

		public static Channel Get(int channelID, PersistenceConnection connection = null)
		{
			var results = Queries.Get.Start()
				.Input<int>("channelID", channelID)
				.Connect(connection)
				.Execute();

			Channel result = null;
			foreach (Channel r in results)
			{
				result = r;
				break;
			}

			return result;
		}
	}
}
