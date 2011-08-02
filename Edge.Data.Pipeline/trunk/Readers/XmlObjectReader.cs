using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.IO;
using System.Collections;
using GotDotNet.XPath;

namespace Edge.Data.Pipeline
{
	public class XmlObjectReader<T> : ReaderBase<T> where T: class
	{
		#region Members
		/*=========================*/

		public Func<XmlReader, T> OnObjectRequired = null;
		private string _url;
		private Stream _stream;
		private string _xpath;
		private bool _ignoreNamespaces = true;
		private XmlReader _xmlReader = null;
		private XmlReaderSettings _settings = null;

		/*=========================*/
		#endregion

		#region Implementation
		/*=========================*/

		public XmlObjectReader(string url, string xpath = null, XmlReaderSettings settings = null)
		{
			if (String.IsNullOrEmpty(url))
				throw new ArgumentNullException("url");

			_url = url;
			_xpath = xpath;
			_settings = settings;
		}

		public XmlObjectReader(Stream stream, string xpath = null, XmlReaderSettings settings = null)
		{
			if (stream == null)
				throw new ArgumentNullException("stream");

			_stream = stream;
			_xpath = xpath;
			_settings = settings;
		}

		/// <summary>
		/// Gets or sets the XPath used to find relevant nodes.
		/// </summary>
		public string XPath
		{
			get { return _xpath; }
			set
			{
				if (_xmlReader != null)
					throw new InvalidOperationException("Cannot change XPath after the reader has started reading.");

				_xpath = value;
			}
		}

		public bool IgnoreNamespaces
		{
			get
			{
				return _ignoreNamespaces;
			}
			set
			{
				if (_xmlReader != null)
					throw new InvalidOperationException("Cannot change IgnoreNamespaces after the reader has started reading.");
				_ignoreNamespaces = true;
			}
		}

		public XmlReader InnerReader
		{
			get { return _xmlReader; }
		}

		protected override void Open()
		{
			if (_xpath == null)
			{
				_xmlReader = _stream != null ?
					XmlTextReader.Create(_stream, _settings) :
					XmlTextReader.Create(_url, _settings);
			}
			else
			{
				var xpaths = new XPathCollection();
				xpaths.Add(_xpath);

				var inner = _stream != null ?
					new XmlTextReader(_stream) :
					new XmlTextReader(_url);

				inner.Namespaces = !_ignoreNamespaces;
				if (_settings != null)
				{
					// TODO: map more XmlReaderSettings to XmlTextReader properties (check in reflector)
					if (_settings.IgnoreWhitespace)
						inner.WhitespaceHandling = WhitespaceHandling.None;
					inner.DtdProcessing = _settings.DtdProcessing;
				}

				_xmlReader = new XPathReader( inner, xpaths );
			}
		}

		protected override bool Next(ref T next)
		{
			if (OnObjectRequired == null)
				throw new InvalidOperationException("A delegate must be specified for OnObjectRequired.");

			if (_xmlReader is XPathReader)
			{
				// if not match, no need to continue
				if (!((XPathReader)_xmlReader).ReadUntilMatch())
					return false;
			}

			next = OnObjectRequired(_xmlReader);
			return next != null;

		}

		public override void Dispose()
		{
			if (_xmlReader != null)
				_xmlReader.Close();
		}

		/*=========================*/
		#endregion
	}
}
