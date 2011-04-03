using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.IO;
using System.Collections;

namespace Edge.Data.Pipeline
{
	public class XmlRowReader<RowT> : RowReader<RowT> where RowT: class
	{
		#region Members
		/*=========================*/

		public Func<XmlTextReader, RowT> OnNextRowRequired = null;
		private string _url;
		private XmlTextReader _xmlReader = null;

		/*=========================*/
		#endregion

		#region Implementation
		/*=========================*/

		public XmlRowReader(string url)
		{
			if (String.IsNullOrEmpty(url))
				throw new ArgumentNullException("url");
			_url = url;
		}

		protected XmlTextReader XmlReader
		{
			get { return _xmlReader; }
		}

		protected override void Open()
		{
			_xmlReader = new XmlTextReader(_url);
			_xmlReader.WhitespaceHandling = WhitespaceHandling.None;
		}

		protected override RowT NextRow()
		{
			if (OnNextRowRequired == null)
				throw new InvalidOperationException("A delegate must be specified for OnNextRowRequired.");

			return OnNextRowRequired(this.XmlReader);
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
