using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.IO;
using System.Collections;
using GotDotNet.XPath;
using DataStreams.Csv;

namespace Edge.Data.Pipeline.Readers
{
	public class CsvObjectReader<T> : ReaderBase<T> where T: class
	{
		#region Members
		/*=========================*/

		public Func<CsvReader, T> OnObjectRequired = null;
		private string _url;
		private char _delimeter;
		private Encoding _encoding;
		private CsvReader _csvReader = null;

		/*=========================*/
		#endregion

		#region Implementation
		/*=========================*/

		public CsvObjectReader(string url, char delimeter = ',', Encoding encoding = null)
		{
			if (String.IsNullOrEmpty(url))
				throw new ArgumentNullException("url");

			_url = url;
			_delimeter = delimeter;
			_encoding = encoding ?? Encoding.UTF8;
		}

		protected CsvReader InnerReader
		{
			get { return _csvReader; }
		}

		protected override void Open()
		{
			_csvReader = new CsvReader(_url, _delimeter, _encoding);
		}

		protected override bool Next(ref T next)
		{
			if (OnObjectRequired == null)
				throw new InvalidOperationException("A delegate must be specified for OnObjectRequired.");

			next = OnObjectRequired(_csvReader);
			return next != null;

		}

		public override void Dispose()
		{
			if (_csvReader != null)
				_csvReader.Close();
		}

		/*=========================*/
		#endregion
	}
}
