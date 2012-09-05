using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eggplant2.Model;

namespace Eggplant2.Queries
{
	public abstract class FilterExpression
	{
		internal static FilterExpression FromObject(object filter)
		{
			if (filter is IEntityProperty)
				return new PropertyFilterExpression() { FilterProperty = (IEntityProperty)filter };
			else if (filter is FilterExpression)
				return (FilterExpression)filter;
			else
				return new GeneralFilterExpression() { FilterValue = filter };
		}
	}

	public class PropertyFilterExpression : FilterExpression
	{
		public IEntityProperty FilterProperty;
	}

	public class GeneralFilterExpression : FilterExpression
	{
		public object FilterValue;
	}
}
