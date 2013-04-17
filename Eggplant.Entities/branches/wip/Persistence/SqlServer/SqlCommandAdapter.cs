using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SqlClient;
using System.Data.Sql;

namespace Eggplant.Entities.Persistence.SqlServer
{
	public class SqlCommandAdapter: PersistenceAdapter
	{
		public SqlCommand Command { get; private set; }
		public SqlDataReader Reader { get; private set; }

		public SqlCommandAdapter(SqlCommandAction action) : base(action)
		{
			this.Command = new SqlCommand(action.CommandText);
			this.Command.CommandType = action.CommandType;

			foreach (PersistenceParameter param in action.Parameters.Values)
			{
				var p = new SqlParameter()
				{
					ParameterName = param.Name,
					Value = param.Value
				};

				if (param.Options != null)
				{
					var options = (SqlPersistenceParameterOptions)param.Options;
					if (options.Size != null)
						p.Size = options.Size.Value;
					if (options.SqlDbType != null)
						p.SqlDbType = options.SqlDbType.Value;
				}

				this.Command.Parameters.Add(p);
			}
		}

		public new SqlCommandAction Action
		{
			get { return (SqlCommandAction)base.Action; }
		}

		public new SqlPersistenceConnection Connection
		{
			get { return (SqlPersistenceConnection)base.Connection; }
		}

		public override bool IsReusable
		{
			get { return true; }
		}

		public override bool HasField(string field)
		{
			for (int i = 0; i < this.Reader.FieldCount; i++)
				if (this.Reader.GetName(i) == field)
					return true;

			return false;
		}

		public override object GetField(string field)
		{
			try
			{
				object val = this.Reader[field];
				if (val is DBNull)
					val = null;
				return val;
			}
			catch (IndexOutOfRangeException ex)
			{
				throw new MappingException(String.Format("Field '{0}' not preset in the SQL results.", field), ex);
			}
		}

		public override void SetField(string field, object value)
		{
			throw new InvalidOperationException("Cannot set field on a data reader.");
		}

		public override void Dispose()
		{
			if (this.Reader != null)
				this.Reader.Dispose();
		}

		public override bool NextResult()
		{
			return this.Reader.Read();
		}

		public override bool NextResultSet()
		{
			return this.Reader.NextResult();
		}

		public override bool HasParam(string param)
		{
			return this.Command.Parameters.Contains(param);
		}

		public override object GetParam(string param)
		{
			return this.Command.Parameters[param].Value;
		}

		public override void SetParam(string param, object value)
		{
			this.Command.Parameters[param].Value = value;
		}

		public override void ExecuteAction()
		{
			this.Command.Connection = this.Connection.DbConnection;

			// Apply parameter values before execute
			foreach (PersistenceParameter param in this.Action.Parameters.Values)
				this.Command.Parameters[param.Name].Value = param.Value;

			this.Reader = this.Command.ExecuteReader();
		}
	}
}
