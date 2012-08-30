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
	public static void GetTablesNamesByAccountID(SqlInt32 accountID)
	{
		try
		{
			using (SqlConnection conn = new SqlConnection("context connection=true"))
			{
				conn.Open();

				StringBuilder sb = new StringBuilder();

				foreach (Type type in typeof(Creative).Assembly.GetTypes())
				{
					if (!type.Equals(typeof(Segment)))
						if (type.IsSubclassOf(typeof(EdgeObject)) && !type.IsAbstract)
						{
							sb.Append(string.Format("SELECT '{0}' Union ", type.Name));
						}
				}
				sb.Append("(SELECT distinct [Name] From MetaProperty where AccountID in(@accountID,-1))");
				SqlCommand cmd = new SqlCommand(sb.ToString());
				SqlParameter account = new SqlParameter("@accountID", accountID);

				cmd.Parameters.Add(account);

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
	public static void GetDataByVirtualTableName(SqlInt32 accountID, SqlString virtualTableName, SqlString deliveryOutputID, SqlDateTime dateCreated)
	{
		string dbtableName = string.Empty;
		string sqlAssembly = typeof(Creative).Assembly.FullName;
		string classNamespace = typeof(Creative).Namespace;
		DummyMapper mapper = new DummyMapper();
		bool isMetaProperty = false;


		#region Getting Type from table name
		/****************************************************************/
		Type type = Type.GetType(string.Format("{0}.{1},{2}", classNamespace, virtualTableName.Value, sqlAssembly));

		if (type != null)
		{
			//Check if subclass of creative / Target / EdgeObject
			if (type.IsSubclassOf(typeof(Creative)))
			{
				dbtableName = typeof(Creative).Name;
			}
			else if (type.IsSubclassOf(typeof(Target)))
			{
				dbtableName = typeof(Target).Name;
			}
			else if (type.IsSubclassOf(typeof(EdgeObject)))
			{
				dbtableName = typeof(EdgeObject).Name;
			}

		}
		else // EdgeObject Type
		{
			isMetaProperty = true;
			dbtableName = typeof(EdgeObject).Name;
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
		if (type != null)
			foreach (var mapItem in mapper.Mapping[type])
			{
				col.Append(dbtableName + ".[");
				col.Append(mapItem.Value);
				col.Append("] as ");
				col.Append("[");
				col.Append(mapItem.Key);
				col.Append("],");
			}

		col.Remove(col.Length - 1, 1);
		//col.Append("[ObjectTracking_Table].[DeliveryOutputID]");


		col.Append(string.Format(" From {0}", dbtableName));


		//JOIN WITH Meta Property Table
		//col.Append(" INNER JOIN ObjectTracking [ObjectTracking_Table]");
		//col.Append(string.Format(" ON [ObjectTracking_Table].ObjectGK = {0}.GK", dbtableName));




		#region Where query string
		/*****************************************************************/

		Int32 metaPropertyID = -1;
		string baseValueType = string.Empty;

		col.Append(" WHERE ");
		if (!isMetaProperty)
		{
			col.Append(" [ObjectType] = @objectType ");
		}
		//Get data from Meta Property Table
		else
		{
			baseValueType = GetMetaPropertyBaseValueType(virtualTableName.Value, accountID.IsNull == true ? SqlInt32.Null : accountID, out metaPropertyID);

			if (string.IsNullOrEmpty(baseValueType))
				return;

			Type baseType = Type.GetType(string.Format("{0}.{1},{2}", classNamespace, baseValueType, sqlAssembly));


			string metaPropertyFieldName = mapper.Mapping[baseType]["MetaPropertyID"];

			col.Append(" ObjectType = @ObjectType");
			col.Append(string.Format(" AND {0} = @MetaPropertyID ", metaPropertyFieldName));

		}

		if (!accountID.IsNull)
			col.Append(" AND AccountID = @accountID ");

		if (!deliveryOutputID.IsNull)
			col.Append(" AND deliveryOutputID = @deliveryOutputID");

		if (!dateCreated.IsNull)
			col.Append(" AND dateCreated = @dateCreated");



		/*****************************************************************/
		#endregion
		/****************************************************************/
		#endregion



		#region Sql Command and parameters
		/*******************************************************************************/
		SqlCommand cmd = new SqlCommand(col.ToString());


		SqlParameter sql_account;
		SqlParameter sql_OutputID;
		SqlParameter sql_dateCreated;

		if (!isMetaProperty)
		{
			SqlParameter sql_objectType = new SqlParameter("@objectType", type.Name);
			cmd.Parameters.Add(sql_objectType);
		}
		else
		{
			SqlParameter sql_objectBaseType = new SqlParameter("@objectType", baseValueType);
			cmd.Parameters.Add(sql_objectBaseType);

			SqlParameter sql_metaPropertyID = new SqlParameter("@MetaPropertyID", metaPropertyID);
			cmd.Parameters.Add(sql_metaPropertyID);
		}

		if (!accountID.IsNull)
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
	public static void GetTableStructureByName(SqlString virtualTableName)
	{
		string sqlAssembly = typeof(Creative).Assembly.FullName;
		string classNamespace = typeof(Creative).Namespace;
		DummyMapper mapper = new DummyMapper();

		#region Getting Type from table name
		/****************************************************************/
		Type type = Type.GetType(string.Format("{0}.{1},{2}", classNamespace, virtualTableName.Value, sqlAssembly));

		if (type == null)
		{
			Int32 metaPropertyID = 0;
			string baseValueType = GetMetaPropertyBaseValueType(virtualTableName.Value, SqlInt32.Null, out metaPropertyID);

			if (string.IsNullOrEmpty(baseValueType))
				return;

			type = Type.GetType(string.Format("{0}.{1},{2}", classNamespace, baseValueType, sqlAssembly));
		}
		/****************************************************************/
		#endregion

		//Creating Select by Dummy table name
		StringBuilder col = new StringBuilder();
		#region Members
		/*******************************************************/

		Dictionary<string, string> tableStructure = GetTableStructure(type);

		foreach (MemberInfo member in type.GetMembers())
		{
			if (IsRelevant(member))
			{
				//Get Params and types
				string sql_name = string.Empty;
				string sql_type = string.Empty;
				string dotNet_name = string.Empty;
				string dotNet_type = string.Empty;
				bool isEnum = false;


				if (((FieldInfo)(member)).FieldType.FullName.Contains(classNamespace))//Memeber is class member from Edge.Data.Object
				{
					//Getting Enum Types
					if ((((FieldInfo)(member)).FieldType).BaseType == typeof(Enum))
					{
						sql_name = member.Name;
						sql_type = tableStructure[mapper.GetMap(type, member.Name)];
						dotNet_name = member.Name;
						dotNet_type = ((MemberInfo)(((FieldInfo)(member)).FieldType)).Name;
						isEnum = true;
					}

					//Getting Types that are not Enum 
					if ((((FieldInfo)(member)).FieldType).BaseType != typeof(Enum))
					{
						sql_name = member.Name + "ID";
						sql_type = tableStructure[sql_name];
						dotNet_name = member.Name + ".ID";
						dotNet_type = ((MemberInfo)(((FieldInfo)(member)).FieldType)).Name;
					}
				}

				else
				{
					sql_name = mapper.GetMap(type, member.Name);
					sql_type = tableStructure[sql_name];
					dotNet_name = member.Name;
					dotNet_type = ((MemberInfo)(((FieldInfo)(member)).FieldType)).Name;


				}




				//Creating sql select query
				col.Append(string.Format(" Select '{0}', '{1}', '{2}', '{3}', '{4}' Union ",
												sql_name,
												sql_type,
												dotNet_name,
												dotNet_type,
												isEnum
											)
							  );
			}
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

	private static bool IsRelevant(MemberInfo member)
	{
		if (member.Name.Equals("MetaProperties")) return false;
		else if (member.MemberType == MemberTypes.Constructor || member.MemberType == MemberTypes.Method
				|| member.Name.Equals("MetaProperty")
			)
			return false;
		else
			return true;
	}

	private static Dictionary<string, string> GetTableStructure(Type type)
	{
		Dictionary<string, string> structure = new Dictionary<string, string>();
		string dbtableName = string.Empty;
		bool isMetaProperty = false;

		if (type != null)
		{
			//Check if subclass of creative / Target / EdgeObject
			if (type.IsSubclassOf(typeof(Creative)))
			{
				dbtableName = typeof(Creative).Name;
			}
			else if (type.IsSubclassOf(typeof(Target)))
			{
				dbtableName = typeof(Target).Name;
			}
			else if (type.IsSubclassOf(typeof(EdgeObject)))
			{
				dbtableName = typeof(EdgeObject).Name;
			}

		}
		else // EdgeObject Type
		{
			isMetaProperty = true;
			dbtableName = typeof(EdgeObject).Name;
		}

		SqlCommand cmd = new SqlCommand("select COLUMN_NAME , DATA_TYPE from information_schema.COLUMNS where TABLE_NAME = @tableName");
		SqlParameter tableName = new SqlParameter("@tableName", dbtableName);

		cmd.Parameters.Add(tableName);

		try
		{
			using (SqlConnection conn = new SqlConnection("context connection=true"))
			{
				conn.Open();
				cmd.Connection = conn;

				using (SqlDataReader reader = cmd.ExecuteReader())
				{
					while (reader.Read())
					{
						structure.Add(reader[0].ToString(), reader[1].ToString());
					}
				}
			}
		}
		catch (Exception e)
		{
			throw new Exception("Could not get table data", e);
		}

		return structure;
	}
	private static string GetMetaPropertyBaseValueType(string metaPropertyName, SqlInt32 accountID, out Int32 metaPropertyID)
	{

		string metaPropertyBaseValueType = string.Empty;

		metaPropertyID = 0;
		StringBuilder cmdSb = new StringBuilder();
		cmdSb.Append("Select [ID], [BaseValueType] from MetaProperty where ");

		if (!accountID.IsNull)
			cmdSb.Append(" AccountID = @accountID and ");

		cmdSb.Append(" Name = @metaPropertyName");

		SqlCommand cmd = new SqlCommand(cmdSb.ToString());

		SqlParameter sql_account = new SqlParameter("@accountID", accountID);
		SqlParameter sql_metaPropertyName = new SqlParameter("@metaPropertyName", metaPropertyName);

		cmd.Parameters.Add(sql_account);
		cmd.Parameters.Add(sql_metaPropertyName);

		try
		{
			using (SqlConnection conn = new SqlConnection("context connection=true"))
			{
				conn.Open();
				cmd.Connection = conn;

				using (SqlDataReader reader = cmd.ExecuteReader())
				{
					if (reader.Read())
					{
						metaPropertyBaseValueType = reader[1].ToString();
						metaPropertyID = Convert.ToInt32(reader[0]);
					}
				}
			}
		}
		catch (Exception e)
		{
			throw new Exception("Could not get table data", e);
		}

		return metaPropertyBaseValueType;
	}
}
