using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Text;
using Eggplant.Entities.Model;
using Eggplant.Entities.Queries;

namespace Eggplant.Entities.Persistence
{
	public interface IMapping
	{
		EntitySpace EntitySpace { get; }
		IMapping ParentMapping { get; }
		IList<IMapping> SubMappings { get; }

		MappingContext CreateContext(MappingContext baseContext);
	}

	public interface IVariableMapping: IMapping
	{
		string Variable { get; }
	}

	public interface IPropertyMapping : IMapping
	{
		IEntityProperty Property { get; }
	}

	public interface IActionMapping : IMapping
	{
		void Execute(MappingContext context);
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
