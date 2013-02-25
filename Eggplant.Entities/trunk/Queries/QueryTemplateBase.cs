using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Eggplant.Entities.Queries
{
	public abstract class QueryTemplateBase : QueryBaseInternal
	{
		public EntitySpace EntitySpace { get; private set; }

		internal QueryTemplateBase(EntitySpace space)
		{
			this.EntitySpace = space;
		}

		public void Param<V>(string paramName, bool required = true, V defaultValue = default(V), V emptyValue = default(V))
		{
			this.Parameters[paramName] = new QueryParameter()
			{
				Name = paramName,
				ParameterType = typeof(V),
				IsRequired = required,
				DefaultValue = defaultValue,
				EmptyValue = emptyValue,
				Value = defaultValue
			};
		}
	}

}
