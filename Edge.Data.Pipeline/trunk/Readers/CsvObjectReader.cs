using System;
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
		private string[] _requiredColumns;
		private bool _headersFound=false;

		/*=========================*/
		#endregion

		#region Implementation
		/*=========================*/

		public CsvObjectReader(string url, string[] requiredColumns, char delimeter = ',', Encoding encoding = null)
		{
			if (String.IsNullOrEmpty(url))
				throw new ArgumentNullException("url");

			_url = url;
			Init(requiredColumns, delimeter, encoding);
		}

		public CsvObjectReader(Stream csvStream, string[] requiredColumns, char delimeter = ',', Encoding encoding = null)
		{
			if (csvStream == null)
				throw new ArgumentNullException("csvStream");

			_csvStream = csvStream;
			Init(requiredColumns, delimeter, encoding);
		}

		private void Init(string[] requiredColumns, char delimeter, Encoding encoding)
		{
			_delimeter = delimeter;
			_encoding = encoding ?? Encoding.UTF8;
			_requiredColumns = requiredColumns;
			MatchExactColumns = true;
			HeaderSearchRange = 20;
		}
		
		public string[] Columns
		{
			get { return _csvReader.Headers; }
		}

		protected CsvReader InnerReader
		{
			get { return _csvReader; }
		}

		public bool MatchExactColumns
		{
			get;
			set;
		}

		public int HeaderSearchRange
		{
			get;
			set;
		}

		protected override void Open()
		{
			if (string.IsNullOrEmpty(_url))
				_csvReader = new CsvReader(_csvStream, _delimeter, _encoding);
			else
				_csvReader = new CsvReader(_url, _delimeter, _encoding);
		}

		protected override bool Next(ref T next)
		{
			if (OnObjectRequired == null)
				throw new InvalidOperationException("A delegate must be specified for OnObjectRequired.");

			//Find Headers
			
			string[] headers=null;
			int rowIndex=0;
			while (!_headersFound && rowIndex <= HeaderSearchRange)
			{
				_csvReader.ReadHeaders();
				headers = _csvReader.Headers;
				int requiredHeadersCount = 0;
				foreach (string header in headers)
				{
					if (_requiredColumns.Contains(header))
						if (!MatchExactColumns)
						{
							_headersFound = true;
							break;
						}
						else
						{
							requiredHeadersCount++;
						}					
				}
				if (requiredHeadersCount==_requiredColumns.Length)
				{
					_headersFound = true;
				}
				rowIndex++;				
			}
			if (_headersFound && _csvReader.ReadRecord())				
					next = OnObjectRequired(_csvReader, _csvReader.Headers, _csvReader.Values);
				else
					next = null;
			
			return next!=null;

		}
		/// <summary>
		/// Skip Line with out parsing any data
		/// </summary>
		/// <returns>true - if succeed , false if failed ( no rows)</returns>
		public bool SkipLine()
		{
			if (_csvReader == null)
				Open();
			return _csvReader.SkipLine();
		}
		/// <summary>
		/// Set the headers
		/// </summary>
		/// <returns>true- headers is set,false - header not set</returns>
		public string[] ReadHeaders()
		{
			if (_csvReader == null)
				Open();
			_csvReader.ReadHeaders();
			return _csvReader.Headers;
		}
		

		public override void Dispose()
		{
			if (_csvReader != null)
				_csvReader.Close();
		}

		/*=========================*/
		#endregion
	}


	[Serializable]
	public class CsvException : Exception
	{
		public CsvException() { }
		public CsvException(string message) : base(message) { }
		public CsvException(string message, Exception inner) : base(message, inner) { }
		protected CsvException(
		  System.Runtime.Serialization.SerializationInfo info,
		  System.Runtime.Serialization.StreamingContext context)
			: base(info, context) { }
	}

}
