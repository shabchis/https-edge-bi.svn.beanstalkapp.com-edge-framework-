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
		public CsvDynamicReader(string url,string[] requiredColumns, char delimeter = ',', Encoding encoding = null)
			: base(url,requiredColumns, delimeter, encoding)
		{
			this.OnObjectRequired = ReadRow;
		}
		public CsvDynamicReader(Stream csvStream, string[] requiredColumns,char delimeter = ',', Encoding encoding = null)
			: base(csvStream,requiredColumns, delimeter, encoding)
		{
			this.OnObjectRequired = ReadRow;	
		}

		dynamic ReadRow(object reader, string[] headers, string[] values)
		{
			
			dynamic obj = new ExpandoObject();

			if (headers.Length==0)
				throw new Exception("No Headers"); //TODO: TALK WITH DORON
			for (int i = 0; i < headers.Length; i++)
			{			 
			
				string name;
				if (headers[i].Contains(" "))
					name = headers[i].Replace(" ", "_");
				else
					name = headers[i];

				((IDictionary<string, object>)obj).Add(name, values.Length <= i ? string.Empty : values[i]);				
				
			}
			return obj;
		}
		 
	}


}
