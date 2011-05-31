﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.IO;
using System.Collections;
using GotDotNet.XPath;
using DataStreams.Csv;

namespace Edge.Data.Pipeline
{
	public class CsvObjectReader<T> : ReaderBase<T> where T : class
	{
		#region Members
		/*=========================*/

		public Func<object, string[], string[], T> OnObjectRequired = null;
		private string _url;
		private char _delimeter;
		private Encoding _encoding;
		private CsvReader _csvReader = null;
		private Stream _csvStream;

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
		public CsvObjectReader(Stream csvStream, char delimeter = ',', Encoding encoding = null)
		{
			if (csvStream == null)
				throw new ArgumentNullException("csvStream");
			_delimeter = delimeter;
			_encoding = encoding ?? Encoding.UTF8;
			_csvStream = csvStream;

		}


		public string[] Columns
		{
			get { return _csvReader.Headers; }
		}

		protected CsvReader InnerReader
		{
			get { return _csvReader; }
		}

		protected override void Open()
		{
			if (string.IsNullOrEmpty(_url))
				_csvReader = new CsvReader(_csvStream, _delimeter,_encoding);
			else
				_csvReader = new CsvReader(_url, _delimeter, _encoding);

		}

		protected override bool Next(ref T next)
		{
			if (OnObjectRequired == null)
				throw new InvalidOperationException("A delegate must be specified for OnObjectRequired.");

			next = OnObjectRequired(_csvReader, _csvReader.Headers, _csvReader.Values);
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
