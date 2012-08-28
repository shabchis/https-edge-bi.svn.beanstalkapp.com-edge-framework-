using System;
using System.Data;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using Microsoft.SqlServer.Server;
using Edge.Data.Objects;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Reflection;


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
		string sqlAssembly = typeof(Creative).Assembly.FullName;
		string classNamespace = typeof(Creative).Namespace;
		DummyMapper mapper = new DummyMapper();

		#region Getting Type from table name
		/****************************************************************/
		Type type = Type.GetType(string.Format("{0}.{1},{2}", classNamespace, dummyTableName.Value, sqlAssembly));

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
		/****************************************************************/
		#endregion

		//Creating Select by Dummy table name
		StringBuilder col = new StringBuilder();
		col.Append("SELECT ");

		#region Members by Type Mapper
		/*******************************************************/
		foreach (var mapItem in mapper.Mapping[typeof(Edge.Data.Objects.EdgeObject)])
		{
			col.Append(dbtableName + ".[");
			col.Append(mapItem.Value);
			col.Append("] as ");
			col.Append("[");
			col.Append(mapItem.Key);
			col.Append("],");
		}
		foreach (var mapItem in mapper.Mapping[type])
		{
			col.Append(dbtableName + ".[");
			col.Append(mapItem.Value);
			col.Append("] as ");
			col.Append("[");
			col.Append(mapItem.Key);
			col.Append("],");
		}

		col.Append("[ObjectTracking_Table].[DeliveryOutputID]");

		/*******************************************************/
		#endregion

		col.Append(string.Format(" From {0}", dbtableName));

		//JOIN WITH OBJECT TRACKING TABLE
		col.Append(" INNER JOIN ObjectTracking [ObjectTracking_Table]");
		col.Append(string.Format(" ON [ObjectTracking_Table].ObjectGK = {0}.GK", dbtableName));

		#region Where query string
		/*****************************************************************/
		col.Append(" WHERE [ObjectType] = @objectType");
		if (accountID != -1)
			col.Append(" AND AccountID = @accountID ");

		if (!deliveryOutputID.IsNull)
			col.Append(" AND deliveryOutputID = @deliveryOutputID");

		if (!dateCreated.IsNull)
			col.Append(" AND dateCreated = @dateCreated");

		/*****************************************************************/
		#endregion

		#region Sql Command and parameters
		/*******************************************************************************/
		SqlCommand cmd = new SqlCommand(col.ToString());

		SqlParameter sql_account;
		SqlParameter sql_OutputID;
		SqlParameter sql_dateCreated;
		SqlParameter sql_objectType = new SqlParameter("@objectType", type.Name);
		cmd.Parameters.Add(sql_objectType);

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

	[Microsoft.SqlServer.Server.SqlProcedure]
	public static void GetTableMembers(SqlString virtualTableName)
	{
		string dbtableName = string.Empty;
		string sqlAssembly = typeof(Creative).Assembly.FullName;
		string classNamespace = typeof(Creative).Namespace;
		DummyMapper mapper = new DummyMapper();

		#region Getting Type from table name
		/****************************************************************/
		Type type = Type.GetType(string.Format("{0}.{1},{2}", classNamespace, virtualTableName.Value, sqlAssembly));


		if (type == null)
		{
			throw new NotImplementedException();
		}
		/****************************************************************/
		#endregion



		//Creating Select by Dummy table name
		StringBuilder col = new StringBuilder();
		#region Members
		/*******************************************************/
		foreach (MemberInfo member in type.GetMembers())
		{
			if (member.MemberType != MemberTypes.Constructor && member.MemberType != MemberTypes.Method)
				col.Append(string.Format(" Select '{0}', '{1}' Union ", member.Name,((System.Reflection.MemberInfo)(((System.Reflection.FieldInfo)(member)).FieldType)).Name));
		}

		//Removing last "union string"
		col.Remove(col.Length - 5, 5);

		//Creating SQL command 
		SqlCommand cmd = new SqlCommand(col.ToString());

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
		/****************************************************************/
		#endregion
	}

}
