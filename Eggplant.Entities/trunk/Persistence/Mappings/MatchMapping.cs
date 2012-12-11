using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Eggplant.Entities.Persistence
{
	public class MatchMapping<T> : Mapping<T>
	{
		public Func<MappingContext<T>, bool> MatchFunction { get; set; }

		internal MatchMapping(EntitySpace space): base(space)
		{
		}

		bool IInlineMapping.IsMatch(MappingContext context)
		{
			return this.MatchFunction((MappingContext<T>)context);
		}

		public InlineMapping<T> Match(params string[] fields)
		{
			return Match(context => fields.All(field => Object.Equals(context.GetField(field), context.GetVariable("__inline__" + field))));
		}

		public InlineMapping<T> Match(Func<MappingContext<T>, bool> matchFunction)
		{
			this.MatchFunction = matchFunction;
			return this;
		}
	}
}
