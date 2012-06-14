using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Dynamic;

namespace Edge.Data.Pipeline.Mapping
{
	public class MappingContext
	{
		public MappingConfiguration Root;

		internal Dictionary<string, object> FieldValues = new Dictionary<string, object>();
		internal Dictionary<ReadCommand, ReadResult> ReadResults = new Dictionary<ReadCommand, ReadResult>();
	}
}
