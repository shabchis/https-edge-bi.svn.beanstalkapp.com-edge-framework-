using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using Eggplant.Model;

namespace Eggplant.Persistence.Providers.Xml
{
	public class XmlObjectMappings
	{
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
		public string XPath { get; set; }
		public List<ParameterMapping> Mappings { get; set; }
	}

	public class ParameterMapping
	{
		public PropertyDefinition Property { get; set; }
		public QueryParameter QueryParameter { get; set; }
		public string XPathParameter { get; set; }
	}

}