using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eggplant.Persistence;
using Eggplant.Persistence.Providers.SqlServer;
using System.Data.SqlClient;
using System.Data.Common;
using Db4objects.Db4o;
using Eggplant.Persistence.Providers.Xml;
using Eggplant.Model;

namespace TestingPlayground
{
	class Program
	{
		/*
		static void Db4oMain()
		{
			using (IObjectContainer db = Db4oEmbedded.OpenFile("chicken.yap"))
			{
				//db.Store(new Role() { RoleName = "Chicken", Description = "A prime roasted chicken" });
				Console.Write(db.Query<Role>(r => r.RoleName == "chicken")[0].ToString());
			}
			Console.ReadLine();
		}
		*/

		static void Main(string[] args)
		{
			/*
			SqlServerProvider provider = new SqlServerProvider();
			provider.ConnectionString = "Data Source=localhost;Initial Catalog=ReportServer;Integrated Security=True;";
			provider.Name = "local";
			Persistence.RegisterProvider(provider);

			using (Persistence.Connect("local"))
			{
				SqlCommand cmd = SqlServerProvider.CreateCommand("select * from Roles");
				
				using (var reader = new ObjectReader<Role>(cmd, fields => new Role()
				{
					RoleName = fields["RoleName"] as string,
					Description = fields["Description"] as string
				}))
				{
					while (reader.Read())
					{
						Console.WriteLine("{0}\n\n", reader.Current);
					}
				}
			}

			Console.ReadLine();
			*/

			ObjectModel model = ObjectModel.Load(string.Empty);

			/*
			XmlProvider provider = new XmlProvider();
			provider.Mappings = mappings;
			provider.FileName = @"C:\example.xml";

			using (Persistence.Connect(provider))
			{
				Persistence.Connection.Save(obj);
			}
			*/
		} 
	}
	
	class Account
	{
		public int ID { get; set; }
		public int Name {get; set;}
		public List<Campaign> Campaigns { get; set; }
		public List<Account> Related { get; set; }
		public List<string> Aliases { get; set; }
	}

	class Campaign
	{
		public long GK { get; set; }
		public string Name { get; set; }
	}

	/*
	class test
	{
		static void Main()
		{
			var provider = new TestProvider();
			provider.Name = "eggplant evolution";

			var map = new ObjectMapping();
			map.Properties["ID"] = new ScalarProperty();
			map.Properties["Campaigns"] = new OwnedListProperty(ListMode.Owner);
			map.Properties["Related"] = new ListProperty(ListMode.Reference);
			map.Properties
		}
	}
	*/
}
