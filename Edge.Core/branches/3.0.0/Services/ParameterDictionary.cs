using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace Edge.Core.Services
{
	[Serializable]
	public class ParameterDictionary: LockableDictionary<string,object>
	{
		public object GetParameter(string paramName, bool emptyIsError = true)
		{
			object val = this[paramName];
			if (emptyIsError && val == null)
				throw new ServiceException(String.Format("The parameter '{0}' is missing in the service configuration.", paramName));
			return val;
		}

		public T GetParameter<T>(string paramName, bool emptyIsError = true, T defaultValue = default(T), Func<object, T> convertFunction = null)
		{
			object raw = this.GetParameter(paramName, emptyIsError);
			T val;
			if (convertFunction != null)
			{
				val = convertFunction(raw);
			}
			else if (raw == null && (!Object.Equals(defaultValue, default(T)) || !emptyIsError))
			{
				val = defaultValue;
			}
			else if (raw is T)
			{
				val = (T)raw;
			}
			else
			{
				TypeConverter converter = TypeDescriptor.GetConverter(typeof(T));
				try { val = (T)converter.ConvertTo(raw, typeof(T)); }
				catch (Exception ex)
				{
					throw new ServiceException(String.Format("The parameter '{0}' could not be converted to {1}. See inner exception for details.", paramName, typeof(T).FullName), ex);
				}
			}

			return val;
		}

	}
}
