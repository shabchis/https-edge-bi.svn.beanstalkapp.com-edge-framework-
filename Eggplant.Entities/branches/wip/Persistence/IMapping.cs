using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Text;
using Eggplant.Entities.Model;
using Eggplant.Entities.Queries;
using System.Collections;

namespace Eggplant.Entities.Persistence
{
	public interface IMapping
	{
		EntitySpace EntitySpace { get; }
		IMapping BaseMapping { get; }
		IMapping ParentMapping { get; }
		IList<IMapping> SubMappings { get; }

		MappingContext CreateContext(MappingContext baseContext);
		MappingContext CreateContext(PersistenceAdapter adapter, Subquery subquery, MappingDirection direction);

		void Apply(MappingContext context);
		void InnerApply(MappingContext context);

		IEnumerable<IMapping> GetAllMappings(bool includeBase = true, Type untilBase = null);
	}

	public interface IChildMapping : IMapping
	{
	}

	public interface IVariableMapping : IChildMapping
	{
		string Variable { get; }
	}

	public interface IPropertyMapping : IChildMapping
	{
		IEntityProperty Property { get; }
	}

	public interface IActionMapping : IMapping
	{
	}

	public interface ISubqueryMapping : IMapping
	{
		string SubqueryName { get; }
	}

	public interface IInlineMapping : IMapping
	{
		bool InGroup(MappingContext context);
	}


}
