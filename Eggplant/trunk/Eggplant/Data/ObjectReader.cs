using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace Eggplant.Data
{

	/// <summary>
	/// 
	/// </summary>
	/// <typeparam name="ThingT"></typeparam>
	public class ObjectReader<T> : IReader
	{
		public Func<Dictionary<string, object>,T> MappingFunction = null;
		T _current;
		IDataReader _reader;

		public ObjectReader(IDbCommand cmd, Func<Dictionary<string, object>,T> mappingFunction = null)
		{
			MappingFunction = mappingFunction;
			_reader = cmd.ExecuteReader();
			
		}

		public T Current
		{
			get
			{
				return _current;
			}
		}

		object IReader.Current
		{
			get { return this.Current; }
		}

		public virtual bool Read()
		{
			if (!_reader.Read())
				return false;
			
			if (MappingFunction == null)
			{
				_current = (T)_reader[0];
			}
			else
			{
				var fields = new Dictionary<string, object>(_reader.FieldCount);
				for (int i = 0; i < _reader.FieldCount; i++)
					fields.Add(_reader.GetName(i), _reader[i]);

				_current = MappingFunction(fields);
			}

			
			return true;
		}

		public virtual void Dispose()
		{
			_reader.Dispose();
		}

	}
}
