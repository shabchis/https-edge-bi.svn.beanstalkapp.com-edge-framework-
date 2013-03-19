using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Eggplant.Entities.Queries
{
	public class QueryParameter
	{
		public string Name;
		public Type ParameterType;
		public bool IsRequired;
		public object DefaultValue;
		public object EmptyValue;
		public object Value;

		public QueryParameter Clone()
		{
			return new QueryParameter()
			{
				Name = this.Name,
				ParameterType = this.ParameterType,
				IsRequired = this.IsRequired,
				DefaultValue = this.DefaultValue,
				EmptyValue = this.EmptyValue,
				Value = this.Value
			};
		}	
	}
}
