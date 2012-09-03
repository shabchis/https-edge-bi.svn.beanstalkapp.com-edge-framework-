using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Core.Configuration;
using System.IO;
using Edge.Core.Services;

namespace Edge.Data.Pipeline
{
	public class CsvDynamicReaderAdapter : ReaderAdapter
	{
		string[] _requiredColumns;
		string _delimeter = null;
		Encoding _encoding = null;

		public override void Init(Stream stream, ServiceConfiguration configuration)
		{
			_requiredColumns = configuration.Parameters.Get<string>("CsvRequiredColumns").Split();
			_delimeter = configuration.Parameters.Get<string>("CsvDelimeter", defaultValue: ",");

			char delimeterCode ;

			switch (_delimeter)
			{
				case "\\t": // Tab code
					delimeterCode = '\t';
					break;
				default: 
					delimeterCode = _delimeter[0];
					break;
			}

			string encoding = configuration.Parameters.Get<string>("CsvEncoding", emptyIsError: false);
			if (!String.IsNullOrEmpty(encoding))
				_encoding = Encoding.GetEncoding(encoding);

			base.Reader = new CsvDynamicReader(stream, _requiredColumns, !String.IsNullOrEmpty(_delimeter) ? delimeterCode : ',', _encoding);
		}

		public new CsvDynamicReader Reader
		{
			get { return (CsvDynamicReader)base.Reader; }
		}

		public override object GetField(string field)
		{
			return this.Reader.Current[field];
		}
	}
}
