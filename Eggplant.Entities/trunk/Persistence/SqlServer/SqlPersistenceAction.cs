using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eggplant.Entities.Queries;
using System.Data.SqlClient;
using System.Data;

namespace Eggplant.Entities.Persistence.SqlServer
{
	public class SqlPersistenceAction: PersistenceAction
	{
		StringBuilder _builder;

		public SqlCommand Command { get; private set; }

		public SqlPersistenceAction()
		{
			this.Command = new SqlCommand();
		}

		public SqlPersistenceAction(string commandText, CommandType commandType)
		{
			this.Command = new SqlCommand(commandText) { CommandType = commandType };
		}

		public new SqlPersistenceConnection Connection
		{
			get { return (SqlPersistenceConnection) base.Connection; }
		}

		public override void Append(PersistenceAction action)
		{
			var ac = (SqlPersistenceAction) action;

			if (_builder == null)
				_builder = new StringBuilder();
			else
				_builder.AppendLine(";");

			// Use the finalized commant text
			_builder.Append(ac.Command.CommandText);

			this.Command.CommandText = _builder == null ? string.Empty : _builder.ToString();
		}

		protected override void OnApplyParameter(PersistenceParameter param)
		{
			SqlParameter p;
			if (this.Command.Parameters.Contains(param.Name))
			{
				p = this.Command.Parameters[param.Name];
			}
			else
			{
				p = new SqlParameter()
				{
					ParameterName = param.Name,
				};

				this.Command.Parameters.Add(p);
			}

			p.Value = param.Value;

			if (param.Options != null)
			{
				var options = (SqlPersistenceParameterOptions)param.Options;
				if (options.Size != null)
					p.Size = options.Size.Value;
				if (options.SqlDbType != null)
					p.SqlDbType = options.SqlDbType.Value;
			}
		}

		public override PersistenceAction Clone()
		{
			return new SqlPersistenceAction()
			{
				Command = this.Command.Clone()
			};
		}

		
		public override PersistenceAdapter GetAdapter(PersistenceAdapterPurpose purpose, MappingDirection mappingDirection)
		{
			/*
			this.Command.Connection = this.Connection.DbConnection;

			if (adapterType == PersistenceAdapterType.ResultSet)
				return new SqlDataReaderAdapter(this, this.Command.ExecuteReader());
			else if (adapterType == PersistenceAdapterType.Parameters)
				return new SqlParameterAdapter(this);
			else
			*/
				throw new NotImplementedException();
		}
	}
}
