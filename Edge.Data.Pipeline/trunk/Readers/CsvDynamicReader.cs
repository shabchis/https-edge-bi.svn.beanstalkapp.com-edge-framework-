using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GotDotNet.XPath;
using System.Xml;
using DataStreams.Csv;
using System.Dynamic;

namespace Edge.Data.Pipeline
{
	public class CsvDynamicReader : CsvObjectReader<dynamic>
	{
		public CsvDynamicReader(string url, char delimeter = ',', Encoding encoding = null)
			: base(url, delimeter, encoding)
		{
			this.OnObjectRequired = ReadRow;
		}

		dynamic ReadRow(object reader, string[] headers, string[] values)
		{
			throw new NotImplementedException();
			//dynamic obj = new object();
			//foreach(string header in headers)
			//	obj[header] = reader.
		}
	}


}
