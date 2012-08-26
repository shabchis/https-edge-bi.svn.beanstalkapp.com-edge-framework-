using System;
using System.Data;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using Microsoft.SqlServer.Server;
using Edge.Data.Objects;
using System.Collections.Generic;
using System.Text;


public partial class StoredProcedures
{
	[Microsoft.SqlServer.Server.SqlProcedure]
	public static void GetTablesNamesByAccountID(SqlInt32 accountID, SqlString customType)
	{
		try
		{
			using (SqlConnection conn = new SqlConnection("context connection=true"))
			{
				conn.Open();
				SqlCommand cmd =
					new SqlCommand(
						"SELECT distinct [ObjectType] From creative where AccountID = @accountID UNION"
						+ " SELECT distinct [ObjectType] From [target] where AccountID = @accountID UNION"
						+ " SELECT distinct [ObjectType] From EdgeObject where [ObjectType] != @custom AND AccountID = @accountID UNION"
						+ " SELECT distinct [Name] From MetaProperty where [BaseValueType] = @custom "
						);

				SqlParameter account = new SqlParameter("@accountID", accountID);
				SqlParameter customObjectType = new SqlParameter("@custom", customType);

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

	[Microsoft.SqlServer.Server.SqlProcedure]
	public static void GetDataByAccountID(SqlInt32 accountID, SqlString dummyTableName, SqlString deliveryOutputID, SqlDateTime dateCreated)
	{
		string dbtableName = string.Empty;

		//Getting Type frin table name 
		Type type = Type.GetType(dummyTableName.Value);
		if (type != null)
		{
			//Check if subclass of creative / Target / EdgeObject
			if (type.IsSubclassOf(typeof(Creative)))
			{
				dbtableName = typeof(Creative).Name;
			}
			if (type.IsSubclassOf(typeof(Target)))
			{
				dbtableName = typeof(Target).Name;
			}

		}
		else // EdgeObject Type
		{
			throw new NotImplementedException();
		}

		//Creating Select by Dummy table name

		StringBuilder col = new StringBuilder();
		col.Append("SELECT ");

		#region Members by Type Mapper
		/*******************************************************/
		foreach (var mapItem in DummyMapper.Mapping[typeof(Edge.Data.Objects.EdgeObject)])
		{
			col.Append("[");
			col.Append(mapItem.Value);
			col.Append("] as ");
			col.Append("[");
			col.Append(mapItem.Key);
			col.Append("],");
		}
		foreach (var mapItem in DummyMapper.Mapping[type])
		{
			col.Append("[");
			col.Append(mapItem.Value);
			col.Append("] as ");
			col.Append("[");
			col.Append(mapItem.Key);
			col.Append("],");
		}

		//removing last comma 
		col.Remove(col.Length - 1, 1);
		/*******************************************************/
		#endregion

		#region Where query string
		/*****************************************************************/
		col.Append(string.Format("From {0} WHERE ", dbtableName));

		bool appendedWhere = false;
		if (accountID != -1)
		{
			col.Append("AccountID = @accountID ");
			appendedWhere = true;
		}

		if (!deliveryOutputID.IsNull)
		{
			if (appendedWhere)
				col.Append("AND ");

			appendedWhere = true;
			col.Append("deliveryOutputID = @deliveryOutputID");
		}

		if (!dateCreated.IsNull)
		{
			if (appendedWhere)
				col.Append("AND ");

			appendedWhere = true;
			col.Append("dateCreated = @dateCreated");
		}
		/*****************************************************************/
		#endregion

		#region Sql Command
		/*******************************************************************************/
		SqlCommand cmd = new SqlCommand(col.ToString());

		SqlParameter sql_account;
		SqlParameter sql_OutputID;
		SqlParameter sql_dateCreated;

		if (accountID != -1)
		{
			sql_account = new SqlParameter("@accountID", accountID);
			cmd.Parameters.Add(sql_account);
		}

		if (!deliveryOutputID.IsNull)
		{
			sql_OutputID = new SqlParameter("@deliveryOutputID", deliveryOutputID);
			cmd.Parameters.Add(sql_OutputID);
		}

		if (!dateCreated.IsNull)
		{
			sql_dateCreated = new SqlParameter("@dateCreated", dateCreated);
			cmd.Parameters.Add(sql_dateCreated);
		}
		/*******************************************************************************/
		#endregion

		try
		{
			using (SqlConnection conn = new SqlConnection("context connection=true"))
			{
				conn.Open();
				cmd.Connection = conn;

				using (SqlDataReader reader = cmd.ExecuteReader())
				{
					SqlContext.Pipe.Send(reader);
				}
			}
		}
		catch (Exception e)
		{
			throw new Exception("Could not get table data", e);
		}

	}
};
