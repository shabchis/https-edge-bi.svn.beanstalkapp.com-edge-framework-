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

		public void Input<V>(string inputName, bool required = true, V defaultValue = default(V), V emptyValue = default(V))
		{
			this.Inputs[inputName] = new QueryInput()
			{
				Name = inputName,
				InputType = typeof(V),
				IsRequired = required,
				DefaultValue = defaultValue,
				EmptyValue = emptyValue,
				Value = defaultValue
			};
		}
	}

}
