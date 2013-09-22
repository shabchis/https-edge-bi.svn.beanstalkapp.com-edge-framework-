using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eggplant.Entities.Queries;
using System.Data.SqlClient;
using System.Data;

namespace Eggplant.Entities.Persistence.SqlServer
{
	public class SqlTextCommand: PersistenceCommand
	{
		public string CommandText { get; private set; }
		public CommandType CommandType { get; private set; }

		public SqlTextCommand()
		{
		}

		public SqlTextCommand(string commandText, CommandType commandType)
		{
			this.CommandText = commandText;
			this.CommandType = commandType;
		}

		public override bool IsAppendable
		{
			get { return true; }
		}

		protected override void OnAppend(PersistenceCommand command)
		{
			var ac = (SqlTextCommand) command;

			var builder = new StringBuilder(this.CommandText);
			if (builder.Length > 0)
				builder.AppendLine(";");
			builder.Append(ac.CommandText);

			// Use the finalized command text
			this.CommandText = builder.ToString();
		}

		public override PersistenceCommand Clone()
		{
			return new SqlTextCommand(this.CommandText, this.CommandType);
		}

		public override PersistenceAdapter GetAdapter(PersistenceConnection connection)
		{
			return new SqlTextCommandAdapter((SqlPersistenceConnection)connection, this);
		}
	}
}
