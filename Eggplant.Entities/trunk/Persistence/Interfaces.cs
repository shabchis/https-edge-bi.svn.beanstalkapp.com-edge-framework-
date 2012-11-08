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
		string ResultSetName { get; }
		IEntityProperty Property { get; }
		MethodInfo InstantiationFunction { get; }
		List<IMapping> SubMappings { get; }
		IMappingContext CreateContext(QueryBase query, MappingDirection direction);
	}

	public interface IMappingContext : IMapping
	{
		QueryBase Query { get; }
		MappingDirection Direction { get; }

		object Target { get; }

		void SetValue(object value);

		V GetField<V>(string field, Func<object, V> convertFunction = null);
		void SetField(string field, object value);
	}


}
