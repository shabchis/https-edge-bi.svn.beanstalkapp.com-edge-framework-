using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Data;
using System.ComponentModel;
using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using Eggplant.Persistence;

namespace Eggplant.Model.Queries
{
	/// <summary>
	/// Query
	/// </summary>
	public class Query: ICloneable
	{
		QueryDefinition _definition;
		PersistenceConnection _connection;
		Dictionary<QueryParameter, object> _parameters = new Dictionary<QueryParameter, object>();

		internal Query(QueryDefinition definition, PersistenceConnection connection)
		{
			_definition = definition;
			_connection = connection;
		}

		public QueryDefinition Definition
		{
			get { return _definition; }
		}

		public PersistenceConnection Connection
		{
			get { return _connection; }
		}

		public Dictionary<QueryParameter, object> Parameters
		{
			get { return _parameters; }
		}

		public void Execute(Dictionary<QueryParameter, object> parameterValues = null)
		{
			this.Execute(QueryReturn.Nothing, parameterValues);
		}

		public object GetValue(Dictionary<QueryParameter, object> parameterValues = null)
		{
			return this.Execute(QueryReturn.Value, parameterValues);
		}

		public object[] GetArray(Dictionary<QueryParameter, object> parameterValues = null)
		{
			return (object[])this.Execute(QueryReturn.Array, parameterValues);
		}

		public IQueryResultReader GetReader(Dictionary<QueryParameter, object> parameterValues = null)
		{
			return (IQueryResultReader)this.Execute(QueryReturn.Reader, parameterValues);
		}

		private object Execute(QueryReturn returnType, Dictionary<QueryParameter, object> parameterValues)
		{
			Query queryToRun;
			if (parameterValues != null && parameterValues.Count > 0)
			{
				queryToRun = this.Clone();

				// Merge object-scope parameter values with method-scope values
				foreach (KeyValuePair<QueryParameter, object> localParam in parameterValues)
					_parameters[localParam.Key] = localParam.Value;
			}
			else
			{
				queryToRun = this;
			}

			return (_connection ?? Persistence.Persistence.Current).ExecuteQuery(queryToRun, returnType);
		}

		public Query Clone()
		{
			Query q = new Query(_definition, _connection);

			// Clone the parameters collection
			q._parameters = this._parameters.ToDictionary
			(
				entry => entry.Key,
				entry => entry.Value
			);

			return q;
		}


		#region ICloneable Members

		object ICloneable.Clone()
		{
			return this.Clone();
		}

		#endregion
	}

	/// <summary>
	/// 
	/// </summary>
	/// <typeparam name="ReturnT"></typeparam>
	public class Query<ReturnT>: Query
	{
		internal Query(QueryDefinition<ReturnT> definition, PersistenceConnection connection):base(definition, connection)
		{
		}

		public new ReturnT GetValue(Dictionary<QueryParameter, object> parameterValues = null)
		{
			return (ReturnT)base.GetValue(parameterValues);
		}

		public new ReturnT[] GetArray(Dictionary<QueryParameter, object> parameterValues = null)
		{
			object[] arr = base.GetArray(parameterValues);
			return arr == null ? null : Array.ConvertAll<object, ReturnT>(arr, o => (ReturnT)o);
		}

	}

}
