using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Db4objects.Db4o;
using Db4objects.Db4o.CS;
using Db4objects.Db4o.Config;
using Db4objects.Db4o.Ext;
using Db4objects.Db4o.Linq;
using Edge.Core.Configuration;
using Edge.Core.Utilities;
using Edge.Core;
using System.Diagnostics;
using System.IO;
using System.Data.Common;



namespace Edge.Data.Pipeline
{
	internal class DeliveryDB
	{
		internal static Delivery Get(Guid deliveryID, bool activate = true, IObjectContainer client = null)
		{
			Delivery delivery;
			bool innerClient = client == null;
			int previousActivationDepth = -1;

			if (innerClient)
				client = DeliveryDBClient.Connect();
			else
				previousActivationDepth = client.Ext().Configure().ActivationDepth();

			try
			{
				// set activation depth to 0 so that only an object reference is retrieved, this can then be used to swap the object
				if (!activate)
					client.Ext().Configure().ActivationDepth(0);

				var results = (from Delivery d in client where d.Guid == deliveryID select d);
				delivery = results.Count() > 0 ? results.First() : null;
			}
			finally
			{
				if (innerClient)
				{
					client.Dispose();
				}
				else
				{
					// reset activation depth
					client.Ext().Configure().ActivationDepth(previousActivationDepth);
				}
			}
			

			return delivery;
		}

		internal static Guid Save(Delivery delivery)
		{
			using (var client = DeliveryDBClient.Connect())
			{
				Guid guid = delivery.Guid;

				if (guid != Guid.Empty)
				{
					// refer here for an explanation on what's going on:
					// http://stackoverflow.com/questions/5848990/retrieve-an-object-in-one-db4o-session-store-in-another-disconnected-scenario

					// Get an inactive reference, get its temp ID and bind the supplied object to it instead
					Delivery inactiveReference = DeliveryDB.Get(delivery.Guid, false, client);
					if (inactiveReference != null)
					{
						long tempDb4oID = client.Ext().GetID(inactiveReference);
						client.Ext().Bind(delivery, tempDb4oID);
					}
				}
				else
				{
					guid = Guid.NewGuid();
				}

				// Try to store, and return new guid on success
				client.Store(delivery);
				return guid;
			}
		}
	}

	internal static class DeliveryDBClient
	{
		static readonly string Host;
		static readonly string User;
		static readonly string Password;
		static readonly int Port;

		static DeliveryDBClient()
		{
			try
			{
				var details = new DbConnectionStringBuilder();
				details.ConnectionString = AppSettings.GetConnectionString(typeof(Delivery), "DB");
				
				Host = (string) details["Host"];
				User = (string) details["User"];
				Password = (string) details["Password"];
				Port = Int32.Parse((string) details["Port"]);
			}
			catch (Exception ex)
			{
				throw new ArgumentException("The supplied connection string is not in the correct format or is missing some parameters (required: Host, User, Password and Port).", ex);
			}
		}

		public static IObjectContainer Connect()
		{
			return Db4oClientServer.OpenClient(Host, Port, User, Password);
		}
	}

	public class DeliveryDBServer:IDisposable
	{
		IObjectServer _server;
		static readonly string File = AppSettings.Get(typeof(Delivery), "Db4o.FileName");
		static readonly string User = AppSettings.Get(typeof(Delivery), "Db4o.User");
		static readonly string Password = AppSettings.Get(typeof(Delivery), "Db4o.Password");
		static readonly int Port = Int32.Parse(AppSettings.Get(typeof(Delivery), "Db4o.Port"));

		public void Start()
		{
			_server = Db4oClientServer.OpenServer(File, Port);
			_server.GrantAccess(User, Password);
		}

		public void Stop()
		{
			_server.Close();
		}

		#region IDisposable Members

		void IDisposable.Dispose()
		{
			Stop();
		}

		#endregion
	}

}
