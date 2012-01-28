using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Pipeline.Mapping
{
	public class ValueLookup
	{
		public string Name;
		public string[] Parameters;
		public Type RequriedType;

		public ValueLookup(string lookupExpression)
		{
			string[] lookup = lookupExpression.Split(':');
			if (lookup.Length < 1)
				throw new MappingConfigurationException("Invalid lookup expression : " + lookupExpression);

			Name = lookup[0];

			if (lookup.Length == 2)
			{
				// TODO: allow escaping the comma
				Parameters = lookup[1].Split(',');
			}
		}
	}
}
