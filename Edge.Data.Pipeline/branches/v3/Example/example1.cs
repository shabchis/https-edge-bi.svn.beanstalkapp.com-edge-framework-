using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SqlClient;
using Easynet.Edge.Core.Data;

namespace Services.Data.Pipeline.Example
{
	class example1
	{
		public example1()
		{
			using (XPathRowReader<MyRowType> reader = new XPathRowReader<MyRowType>(@"C:\myfile.xml", "stats/ad_stats"))
			{
				reader.OnNextRowRequired = interalReader => new MyRowType
				{
					Account = interalReader.GetAttribute("account"),
					Campaign = interalReader.GetAttribute("campaign")
				};

				while (reader.Read())
				{
					SqlCommand sql = DataManager.CreateCommand("insert into ");
					sql.Parameters["@account"].Value = reader.CurrentRow.Account;
					sql.Parameters["@campaign"].Value = reader.CurrentRow.Campaign;
					sql.ExecuteNonQuery();
				}

			}
		}
	}

	public class MyRowType
	{
		public string Account;
		public string Campaign;
	}
}
