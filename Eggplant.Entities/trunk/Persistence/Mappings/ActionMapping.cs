using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Eggplant.Entities.Persistence
{
	public class ActionMapping<T>: Mapping<T>, IActionMapping
	{
		public Action<MappingContext<T>> Action { get; set; }

		internal ActionMapping(IMapping parentMapping, EntitySpace space = null)
			: base(parentMapping, space)
		{
		}

		public override void Apply(MappingContext<T> context)
		{
			if (this.Action == null)
				throw new MappingException("ActionMapping.Action is null and cannot be executed.");

			this.Action(context);
		}

		public override string ToString()
		{
			return String.Format("Action ({0})", typeof(T).Name);
		}
	}
}
