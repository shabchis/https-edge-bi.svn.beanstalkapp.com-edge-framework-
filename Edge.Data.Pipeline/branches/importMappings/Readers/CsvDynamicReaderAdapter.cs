using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Core.Configuration;
using System.IO;

namespace Edge.Data.Pipeline
{
	public class CsvProcessorService : ReaderAdapter
	{
		string[] _requiredColumns;
		string _delimeter;
		Encoding _encoding;

		public override void Init(Stream stream, ServiceElement configuration)
		{
			_requiredColumns = configuration.GetOption("CsvRequiredColumns").Split();
			_delimeter = configuration.Options["CsvDelimeter"];
			string encoding = configuration.Options["CsvEncoding"];
			if (!String.IsNullOrEmpty(encoding))
				_encoding = Encoding.GetEncoding(encoding);

			base.Reader = new CsvDynamicReader(stream, _requiredColumns);
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
