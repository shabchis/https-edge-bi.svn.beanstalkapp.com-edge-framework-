using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.IO;
using System.Collections;
using GotDotNet.XPath;

namespace Edge.Data.Pipeline.Readers
{
	public class XmlObjectReader<T> : ReaderBase<T> where T: class
	{
		#region Members
		/*=========================*/

		public Func<XmlReader, T> OnObjectRequired = null;
		private string _url;
		private string _xpath;
		private XmlReader _xmlReader = null;

		/*=========================*/
		#endregion

		#region Implementation
		/*=========================*/

		public XmlObjectReader(string url, string xpath = null)
		{
			if (String.IsNullOrEmpty(url))
				throw new ArgumentNullException("url");

			_url = url;
			_xpath = xpath;
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

		protected XmlReader InnerReader
		{
			get { return _xmlReader; }
		}

		protected override void Open()
		{
			if (_xpath == null)
			{
				_xmlReader = new XmlTextReader(_url)
				{
					WhitespaceHandling = WhitespaceHandling.None
				};
			}
			else
			{
				_xmlReader = new XPathReader(_url, _xpath);
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
