using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Core.Services;
using System.IO;
using Edge.Core.Services.Configuration;

namespace Edge.Data.Pipeline
{
	/// <summary>
	/// XML metrics processor service.
	/// </summary>
	public class XmlDynamicReaderAdapter:ReaderAdapter
	{
		string _xpath;

		public override void Init(Stream stream, ServiceConfiguration configuration)
		{
			_xpath = configuration.Parameters.Get<string>("XPath");
			base.Reader = new XmlDynamicReader(stream, _xpath);
		}

		public new XmlDynamicReader Reader
		{
			get { return (XmlDynamicReader)base.Reader; }
		}

		public override object GetField(string field)
		{
			if (field.StartsWith("@"))
				return this.Reader.Current.Attributes[field.Substring(1)];
			else
				return this.Reader.Current[field];
		}
	}
}
