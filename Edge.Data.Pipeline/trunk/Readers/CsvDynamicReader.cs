using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GotDotNet.XPath;
using System.Xml;
using DataStreams.Csv;
using System.Dynamic;
using System.IO;

namespace Edge.Data.Pipeline
{
	public class CsvDynamicReader : CsvObjectReader<dynamic>
	{
		public CsvDynamicReader(string url, string[] requiredColumns, char delimeter = ',', Encoding encoding = null)
			: base(url,requiredColumns, delimeter, encoding)
		{
			this.OnObjectRequired = ReadRow;
		}
		public CsvDynamicReader(Stream csvStream, string[] requiredColumns,char delimeter = ',', Encoding encoding = null)
			: base(csvStream,requiredColumns, delimeter, encoding)
		{
			this.OnObjectRequired = ReadRow;	
		}

		dynamic ReadRow(object reader, string[] columns, string[] values)
		{
			dynamic obj = new DynamicDictionaryObject();

			if (columns == null || columns.Length==0)
				throw new CsvException("No columns could be found.");

			for (int i = 0; i < columns.Length; i++)
			{
				string name = columns[i];
				obj[name] = values.Length <= i ? string.Empty : values[i];
			}
			return obj;
		}
		 
	}
}
