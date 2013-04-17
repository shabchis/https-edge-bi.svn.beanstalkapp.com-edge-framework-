using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Eggplant.Entities.Persistence
{
	public class FunctionMapping<T>: Mapping<T>, IActionMapping
	{
		public Action<MappingContext<T>> Function { get; set; }

		internal FunctionMapping(IMapping parentMapping, EntitySpace space = null)
			: base(parentMapping, space)
		{
		}

		public override void Apply(MappingContext<T> context)
		{
			if (this.Function == null)
				throw new MappingException("FunctionMapping.Function is null and cannot be executed.");

			this.Function(context);
		}

		public override string ToString()
		{
			return String.Format("Function ({0})", typeof(T).Name);
		}
	}
}
