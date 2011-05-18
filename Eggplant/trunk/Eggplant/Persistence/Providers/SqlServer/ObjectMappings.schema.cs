using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using Eggplant.Model;

namespace Eggplant.Persistence.Providers.SqlServer
{
	public class SqlServerObjectMappings
	{
		public string ProviderName { get; set; }
		public List<ObjectMapping> ObjectMappings { get; set; }
	}

	public class ObjectMapping
	{
		public ObjectDefinition TargetObject { get; set; }
		public List<QueryMapping> QueryMappings { get; set; }
	}

	public class QueryMapping
	{
		public QueryDefinition TargetQuery { get; set; }
		public List<CommandDefinition> Commands { get; set; }
	}

	public class CommandDefinition
	{
		public string Text { get; set; }
		public CommandType CommandType { get; set; }
		public DataState[] TargetDataState { get; set; }
		public List<CommandParameter> Parameters { get; set; }
		public List<InputOutputMapping> Mappings { get; set; }
	}

	public class CommandParameter
	{
		public string Name { get; set; }
		public Type Type { get; set; }
	}

	public class InputOutputMapping
	{
		public MappingDirection Direction { get; set; }
		public PropertyDefinition Property { get; set; }
		public QueryParameter QueryParameter { get; set; }
		public CommandParameter CommandParameter { get; set; }
		public string ResultField { get; set; }
	}

	public enum MappingDirection
	{
		In,
		Out,
		InAndOut
	}
}
