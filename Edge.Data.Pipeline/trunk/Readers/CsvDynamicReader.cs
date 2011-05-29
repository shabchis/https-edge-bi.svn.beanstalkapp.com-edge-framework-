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
			throw new NotImplementedException();
			//this.OnObjectRequired = ReadRow;
		}

		dynamic ReadRow(CsvReader reader)
		{
			throw new NotImplementedException();
			//dynamic obj = new object();
			//foreach(string header in reader.Headers)
			//	obj[header] = reader.
		}
	}


}
