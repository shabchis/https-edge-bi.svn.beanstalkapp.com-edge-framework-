using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eggplant.Entities.Queries;
using System.Data.SqlClient;
using System.Data;

namespace Eggplant.Entities.Persistence.SqlServer
{
	public class SqlCommandAction: PersistenceAction
	{
		public string CommandText { get; private set; }
		public CommandType CommandType { get; private set; }

		public SqlCommandAction()
		{
		}

		public SqlCommandAction(string commandText, CommandType commandType)
		{
			this.CommandText = commandText;
			this.CommandType = commandType;
		}

		public override bool IsAppendable
		{
			get { return false; }
		}

		protected override void OnAppend(PersistenceAction action)
		{
			var ac = (SqlCommandAction) action;

			var builder = new StringBuilder(this.CommandText);
			if (builder.Length > 0)
				builder.AppendLine(";");
			builder.Append(ac.CommandText);

			// Use the finalized command text
			this.CommandText = builder.ToString();
		}

		public override PersistenceAction Clone()
		{
			return new SqlCommandAction(this.CommandText, this.CommandType);
		}

		public override PersistenceAdapter GetAdapter()
		{
			return new SqlCommandAdapter(this);
		}
	}
}
