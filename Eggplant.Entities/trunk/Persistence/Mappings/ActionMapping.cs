using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Eggplant.Entities.Persistence
{
	public class ActionMapping<T>: Mapping<T>, IActionMapping
	{
		public Action<MappingContext<T>> Action { get; set; }

		internal ActionMapping(EntitySpace space) : base(space)
		{
		}

		void IActionMapping.Execute(MappingContext context)
		{
			if (this.Action == null)
				throw new MappingException("ActionMapping.Action is null and cannot be executed.");

			this.Action((MappingContext<T>)context);
		}

	}
}
