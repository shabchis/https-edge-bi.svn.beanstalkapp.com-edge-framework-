using System;
using System.Data;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using Microsoft.SqlServer.Server;


public partial class StoredProcedures
{
	[Microsoft.SqlServer.Server.SqlProcedure]
	public static void GetTablesNamesByAccountID(SqlInt32 accountID,SqlString customType)
	{
		try
		{
			using (SqlConnection conn = new SqlConnection("context connection=true"))
			{
				conn.Open();
				SqlCommand cmd =
					new SqlCommand(
						"SELECT distinct [ObjectType] From creative where AccountID = @accountID UNION"
						+" SELECT distinct [ObjectType] From [target] where AccountID = @accountID UNION"
						+ " SELECT distinct [ObjectType] From EdgeObject where [ObjectType] != @custom AND AccountID = @accountID UNION"
						+ " SELECT distinct [Name] From MetaProperty where [BaseValueType] = @custom "
						);

				SqlParameter account = new SqlParameter("@accountID",accountID);
				SqlParameter customObjectType = new SqlParameter("@custom",customType);

				cmd.Parameters.Add(account);
				cmd.Parameters.Add(customObjectType);

				cmd.Connection = conn;
				using (SqlDataReader reader = cmd.ExecuteReader())
				{
					SqlContext.Pipe.Send(reader);
				}
			}
		}
		catch (Exception e)
		{
			throw new Exception("Could not get table list from data object data base", e);
		}
	}
};
