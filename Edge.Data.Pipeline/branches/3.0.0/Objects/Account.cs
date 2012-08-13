using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SqlClient;
using Edge.Core.Data;
using Edge.Core.Configuration;

namespace Edge.Data.Objects
{
	public class Account
	{
		public int ID;
		public string OriginalID;
        public string Name;
        public int Status;

        public static Dictionary<int, Account> GetAccounts(SqlConnection connection)
        {
            SqlCommand cmd = DataManager.CreateCommand(
             "SELECT [Account_ID],[Account_Name] FROM [dbo].[User_GUI_Account] order by [Account_ID]");
            cmd.Connection = connection;

            List<Account> accounts = new List<Account>();
            using (SqlDataReader reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    Account a = new Account()
                    {
                     ID = (int)reader["Account_ID"],
                     Name = reader["Account_Name"] is DBNull ? string.Empty : (string)reader["Account_Name"]
                    };
                    accounts.Add(a);
                }
            }
            return accounts.ToDictionary(a => a.ID);
        }
	}
}
