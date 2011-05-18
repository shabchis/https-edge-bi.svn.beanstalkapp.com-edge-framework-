using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SqlClient;
using System.Configuration;
using System.Threading;
using System.Text.RegularExpressions;
using System.Data;
using System.Reflection;

namespace Eggplant.Persistence.Providers.SqlServer
{
	public class SqlServerProvider: PersistenceProvider
	{
		public string ConnectionString
		{
			get; set;
		}

		public SqlServerObjectMappings Mappings
		{
			get;
			set;
		}

		
		protected override PersistenceConnection CreateNewConnection()
		{
			SqlConnection cn = new SqlConnection(ConnectionString);
			return new SqlServerConnection(this, cn);
		}


		#region Helper methods
		/*=========================*/

		static Regex _paramFinder = new Regex(@"\{[^\{]*\}");

		/// <summary>
		/// Parses parameter macros.
		/// The format of a macro is {@paramName: type=Int, dir=In, size=50, ...}
		/// or in abbreviated form: {@paramName:Int}
		/// </summary>
		[Obsolete("Need to update use of regex here.")]
		private static void ApplyParameters(string rawCommandText, SqlCommand command)
		{
			string commandText = rawCommandText;
			int offsetChange = 0;

			// Run the regular expression
			MatchCollection placeHolders = _paramFinder.Matches(rawCommandText);

			// Iterate the matches
			for (int i = 0; i < placeHolders.Count; i++)
			{
				string placeHolder = placeHolders[i].Value.Substring(1, placeHolders[i].Value.Length-2);
				string[] segments = placeHolder.Split(new char[]{':'}, 2, StringSplitOptions.RemoveEmptyEntries);
				
				// Extract the param name
				string name = segments[0].Trim();

				// TODO: make provider-specific param name modifications
				// Add the SQL parameter token
				//if (name[0] != '@')
				//	name = '@' + name;

				// Don't accept invalid names
				if (name.Length < 1)
				{
					// EXCEPTION:
					throw new Exception(String.Format("A parameter requires a valid name: {0}", placeHolders[i].Value));
				}
				
				// Ignore the parameter if it already has been added
				if (command.Parameters.Contains(name))
					continue;

				// Replace placeholder with actual parameter token
				commandText = commandText.Remove(placeHolders[i].Index + offsetChange, placeHolders[i].Length);
				commandText = commandText.Insert(placeHolders[i].Index + offsetChange, name);
				offsetChange += name.Length - placeHolders[i].Length;

				// Add the parameter
				SqlParameter param = new SqlParameter();
				param.ParameterName = name;
				command.Parameters.Add(param);

				// No parameter attributes, so continue
				if (segments.Length < 2)
					continue;

				string[] attributes = segments[1].Split(new char[]{','}, StringSplitOptions.RemoveEmptyEntries);
				foreach (string attribute in attributes)
				{
					string attributeName = null;
					string attributeRaw = null;
					string[] attributeSegments = attribute.Split(new char[]{'='}, 2, StringSplitOptions.RemoveEmptyEntries);
					
					if (attributeSegments.Length < 2)
					{
						// When no attribute name is specified, treat the value as the dbType
						attributeName = "type";
						attributeRaw = attributeSegments[0].Trim();
					}
					else
					{
						attributeName = attributeSegments[0].Trim();
						attributeRaw = attributeSegments[1].Trim();
					}

					// Specially recognized attribute
					bool reflectionRequired = false;
					switch(attributeName.ToLower())
					{
						case "type":
							param.SqlDbType =
								(SqlDbType) Enum.Parse(typeof(SqlDbType), attributeRaw, true); 
							break;
						
						case "dir":
							param.Direction =
								(ParameterDirection) Enum.Parse(typeof(ParameterDirection), attributeRaw, true);
							break;

						default:
							reflectionRequired = true;
							break;
							
					}

					// Attributes that require reflection
					if (reflectionRequired)
					{
						PropertyInfo propertyInfo = param.GetType().GetProperty(attributeName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
						if (propertyInfo == null)
							continue;

						// Convert and apply value
						object attributeValue = Convert.ChangeType(attributeRaw, propertyInfo.PropertyType);
						propertyInfo.SetValue(param, attributeValue, null);
					}
				}

			}

			// For stored procs, leave only the name
			if (command.CommandType == System.Data.CommandType.StoredProcedure)
				commandText = commandText.Split('(')[0];

			// Replace command text to proper version
			command.CommandText = commandText;
		}

		public static SqlCommand CreateCommand(string commandText, CommandType commandType = CommandType.Text)
		{
			SqlCommand cmd = new SqlCommand(commandText);
			cmd.CommandType = commandType;

			if (Persistence.Current is SqlServerConnection)
			{
				var conn = (SqlServerConnection)Persistence.Current;
				cmd.Connection = conn.InternalConnection;
				cmd.Transaction = conn.InternalTransaction;
			}

			return cmd;
		}

		/*=========================*/
		#endregion
	}
}
