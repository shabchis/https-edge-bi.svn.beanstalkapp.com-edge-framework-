using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
//using Db4objects.Db4o;
//using Db4objects.Db4o.CS;
//using Db4objects.Db4o.Config;
//using Db4objects.Db4o.Ext;



namespace Edge.Data.Pipeline.Deliveries
{
	
	internal class DeliveryDB
	{
		private const string _serverName="localhost";
		private const string _userName = "test";
		private const string _password = "123456";
		private const int _port = 999;

		public static Delivery Get(long deliveryID)
		{
			throw new NotImplementedException();
		}

		/*
		public static Delivery Get(Db4oUUID deliveryID) //TODO: TALK DORON WHAT TO DO, WE HAVE TO HAVE SIGNATURE AND LONG (ID)
		{
			Delivery delivery;
			using (IObjectContainer client = Db4oClientServer.OpenClient(_serverName, _port, _userName, _password))
			{
				delivery = (Delivery)client.Ext().GetByUUID(deliveryID);
				client.Ext().Activate(delivery);
				
			}
			return delivery;//TODO: BIG PROBLEM!! IF YOU RETURN DELIVERY AFTER CLOSING THE CONNECTION(CLIENT) THE DB40 WON'T 
			                //  UPDATE ON THE SAVE, BUT IT WILL ADD MORE DELIVERY
		}

		public static void Save(Delivery delivery)
		{		
			//server configuration
			//Db4objects.Db4o.CS.Config.IServerConfiguration serverConfiguration = Db4oClientServer.NewServerConfiguration();
			
			//serverConfiguration.Common.ObjectClass(typeof(Delivery)).GenerateUUIDs(true);
			//using (IObjectServer server = Db4oClientServer.OpenServer(serverConfiguration,"DatabaseFile", 999))
			//{
			//    server.GrantAccess(_userName, _password);
				

			//}


				using (IObjectContainer client = Db4oClientServer.OpenClient(_serverName,_port,_userName,_password))
				{
					client.Store(delivery);
				}
			
		}
	*/
	}
}
