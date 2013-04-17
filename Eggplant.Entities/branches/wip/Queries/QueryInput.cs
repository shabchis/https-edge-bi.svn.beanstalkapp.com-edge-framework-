using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Eggplant.Entities.Queries
{
	public class QueryInput
	{
		public string Name;
		public Type InputType;
		public bool IsRequired;
		public object DefaultValue;
		public object EmptyValue;
		public object Value;

		public QueryInput Clone()
		{
			return new QueryInput()
			{
				Name = this.Name,
				InputType = this.InputType,
				IsRequired = this.IsRequired,
				DefaultValue = this.DefaultValue,
				EmptyValue = this.EmptyValue,
				Value = this.Value
			};
		}	
	}
}
