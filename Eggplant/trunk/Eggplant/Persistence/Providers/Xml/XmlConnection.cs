using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SqlClient;
using System.IO;
using Eggplant.Model.Queries;
using GotDotNet.XPath;

namespace Eggplant.Persistence.Providers.Xml
{
	public class XmlConnection : PersistenceConnection
	{
		public new XmlProvider Provider
		{
			get { return base.Provider as XmlProvider; }
		}

		internal XmlConnection(XmlProvider provider) : base(provider)
		{
		}

		protected override void OnOpen()
		{
		}

		protected override void OnClose()
		{
		}

		public override void TransactionStart()
		{
		}

		public override void TransactionCommit()
		{
		}

		public override void TransactionRollback()
		{
		}

		protected override void ExecuteQueryNoReturn(Query query)
		{
			throw new NotImplementedException("Modification of XML not yet supported by XmlProvider.");
		}

		protected override object ExecuteQueryAsValue(Query query)
		{
			throw new NotImplementedException();
			bool parseXsd = Provider.SchemaUrl == null;

			string xpath = string.Empty;
			XPathReader reader = new XPathReader(Provider.Url, xpath);

		}

		protected override object[] ExecuteQueryAsArray(Query query)
		{
			throw new NotImplementedException();
		}

		protected override IQueryResultReader ExecuteQueryAsReader(Query query)
		{
			throw new NotImplementedException();
		}
	}


}
