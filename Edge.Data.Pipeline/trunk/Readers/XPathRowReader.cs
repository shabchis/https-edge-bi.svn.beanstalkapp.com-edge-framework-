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
	public class XPathRowReader<RowT> : RowReader<RowT> where RowT : class
	{
		#region Members
		/*=========================*/

		public Func<XPathReader, RowT> OnNextRowRequired = null;
		private XPathReader _xpathReader = null;
		private string _url;
		private string _xpath;		

		/*=========================*/
		#endregion

		#region Implementation
		/*=========================*/

		public XPathRowReader(string url, string xpath)
		{
			_url = url;
			_xpath = xpath;
		}

		protected XPathReader XPathReader
		{
			get { return _xpathReader; }
		}

		protected override void Open()
		{
			_xpathReader = new XPathReader(_url, _xpath);
		}

		protected override RowT NextRow()
		{
			if (OnNextRowRequired == null)
				throw new InvalidOperationException("A delegate must be specified for OnNextRowRequired.");

			bool found = _xpathReader.ReadUntilMatch();
			if (!found)
				return null;
			else
				return OnNextRowRequired(XPathReader);
		}

		public override void Dispose()
		{
			if (_xpathReader != null)
				_xpathReader.Close();
		}

		/*=========================*/
		#endregion
	}
}
