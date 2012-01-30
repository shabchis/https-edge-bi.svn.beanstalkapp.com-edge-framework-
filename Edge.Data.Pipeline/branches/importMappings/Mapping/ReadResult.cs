using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Pipeline.Mapping
{
	public class ReadResult: DynamicDictionaryObject
	{
		public string Result;

		public override string ToString()
		{
			return this.Result;
		}
	}
}
