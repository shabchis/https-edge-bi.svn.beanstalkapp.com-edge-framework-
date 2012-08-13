using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SqlClient;
using Edge.Core.Data;
using Edge.Core.Configuration;

namespace Edge.Data.Objects
{
	public class Channel
	{
		public int ID;
        public string Name;

        public static Dictionary<int, Channel> GetChannels(SqlConnection connection)
		{
            SqlCommand cmd = DataManager.CreateCommand(
             "SELECT  [Channel_ID],[Channel_Name] FROM [dbo].[Constant_Channel] order by [Channel_ID]");
            cmd.Connection = connection;

            List<Channel> channels = new List<Channel>();
			using (SqlDataReader reader = cmd.ExecuteReader())
			{
			    while (reader.Read())
				{
				    Channel c = new Channel()
					{
                        ID = (int)reader["Channel_ID"],
                        Name = reader["Channel_Name"] is DBNull ? string.Empty : (string)reader["Channel_Name"],
					};
                    
                    channels.Add(c);
				}
			}

            return channels.ToDictionary(c => c.ID);               
	    }
	}
}
