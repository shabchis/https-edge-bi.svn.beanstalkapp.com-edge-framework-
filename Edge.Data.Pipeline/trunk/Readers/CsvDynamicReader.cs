using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GotDotNet.XPath;
using System.Xml;
using DataStreams.Csv;

namespace Edge.Data.Pipeline.Readers
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
		}
	}

}
