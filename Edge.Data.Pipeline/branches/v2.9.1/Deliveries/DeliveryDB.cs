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
using System.Data.SqlClient;
using Edge.Core.Services;
using Edge.Core.Data;
using Edge.Data.Objects;



namespace Edge.Data.Pipeline
{
	internal class DeliveryDB
	{
		private static class ResultSetIndex
		{
			public const int Delivery = 0;
			public const int DeliveryParameters = 1;
			public const int DeliveryHistory = 2;
			public const int DeliveryHistoryParameters = 3;
			public const int DeliveryFile = 4;
			public const int DeliveryFileParameters = 5;
		}

		internal static Delivery Get(Guid deliveryID, bool deep = true, SqlConnection connection = null)
		{
			Delivery delivery;
			bool innerConnection = connection == null;

			if (innerConnection)
				connection = DeliveryDBClient.Connect();

			try
			{
				SqlCommand cmd = DataManager.CreateCommand("Delivery_Get(@deliveryID:Char, @deep:bit)", System.Data.CommandType.StoredProcedure);
				cmd.Parameters["@deliveryID"].Value = deliveryID.ToString("N");
				cmd.Parameters["@deep"].Value = deep;

				using (SqlDataReader reader = cmd.ExecuteReader())
				{
					int resultSetCount=0;
					while (reader.NextResult())
					{
						resultSetCount++;
						switch (resultSetCount)
						{
							case ResultSetIndex.Delivery:
								delivery = new Delivery(reader.Convert<string,Guid>("DeliveryID", s => Guid.Parse(s)))
								{
									Account = reader.Convert<int?,Account>("AccountID", id => id.HasValue ? new Account(){ID = id.Value} : null),
									Channel = reader.Convert<int?, Channel>("ChannelID", id => id.HasValue ? new Channel() { ID = id.Value } : null),
								};
								break;
							case ResultSetIndex.DeliveryParameters:
								break;
							case ResultSetIndex.DeliveryHistory:
								break;
							case ResultSetIndex.DeliveryHistoryParameters:
								break;
							case ResultSetIndex.DeliveryFile:
								break;
							case ResultSetIndex.DeliveryFileParameters:
								break;
						}
					}
				}
			}
			finally
			{
				if (innerConnection)
				{
					connection.Dispose();
				}
			}
/*
#if DEBUG
			Log.Write(String.Format("Delivery - {3}found: {0} (activate: {2}, {1} results)\n", deliveryID, resultCount, activate, delivery == null ? "not " : "" ), LogMessageType.Information);

#endif			
*/
			return delivery;
		}

		internal static Guid Save(Delivery delivery)
		{
			
			using (var client = DeliveryDBClient.Connect())
			{
				Guid guid = delivery.DeliveryID;

				if (guid != Guid.Empty)
				{
					Delivery inactiveReference = DeliveryDB.Get(delivery.DeliveryID, false, client);
					if (inactiveReference != null)
					{
						// refer here for an explanation on what's going on:
						// http://stackoverflow.com/questions/5848990/retrieve-an-object-in-one-db4o-session-store-in-another-disconnected-scenario
						long tempDb4oID = client.Ext().GetID(inactiveReference);
						client.Ext().Bind(delivery, tempDb4oID);
						//Log.Write(String.Format("Delivery - overwrite (db4o id {0})", tempDb4oID), LogMessageType.Information);
					}
				}
				else
				{
					guid = Guid.NewGuid();
					throw new NotSupportedException("In Pipeline 2.9, you cannot save a Delivery without first giving it a GUID.");
				}

				// Give GUIDs to delivery files
				foreach (DeliveryFile file in delivery.Files)
					if (file.FileID == Guid.Empty)
						file.FileID = Guid.NewGuid();

				// Try to store, and return new guid on success

				client.Store(delivery);
				return guid;
			}
		}

		internal static void Delete(Delivery delivery, IObjectContainer client = null)
		{
			bool close = true;
			if (client == null)
				client = DeliveryDBClient.Connect();
			else
				close = false;

			try
			{
				
				// Get an inactive reference, get its temp ID and bind the supplied object to it instead
				Delivery inactiveReference = DeliveryDB.Get(delivery.DeliveryID, false, client);
				if (inactiveReference != null) {
					client.Delete(inactiveReference);
				}
			}
			finally
			{
				if (close)
					client.Dispose();
			}
		}

		internal static Delivery[] GetBySignature(string signature, Guid[] exclude)
		{
			using (var client = DeliveryDBClient.Connect())
			{
				// Select deliveries that match a signature but none of the guids in 'exclude'
				var results = from Delivery d in client
							  where d.Signature == signature && !exclude.Any(toExclude => d.DeliveryID == toExclude)
							 select d;

				return results.ToArray();
			}
		}

		internal static Delivery[] GetByTargetPeriod(int channelID, int accountID, DateTime start, DateTime end)
		{
			using (var client = DeliveryDBClient.Connect())
			{
				var results = from Delivery d in client
							  where d.Channel.ID == channelID
							  select d;

				return results.ToArray();
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

				Host = (string)details["Host"];
				User = (string)details["User"];
				Password = (string)details["Password"];
				Port = Int32.Parse((string)details["Port"]);
			}
			catch (Exception ex)
			{
				throw new ArgumentException("The supplied connection string is not in the correct format or is missing some parameters (required: Host, User, Password and Port).", ex);
			}
		}

		public static SqlConnection Connect()
		{
			throw new NotImplementedException();
			//return Db4oClientServer.OpenClient(Host, Port, User, Password);
		}
	}

	public class DeliveryDBServer : IDisposable
	{
		IObjectServer _server;
		static readonly string File = AppSettings.Get(typeof(Delivery), "Db4o.FileName");
		static readonly string User = AppSettings.Get(typeof(Delivery), "Db4o.User");
		static readonly string Password = AppSettings.Get(typeof(Delivery), "Db4o.Password");
		static readonly int Port = Int32.Parse(AppSettings.Get(typeof(Delivery), "Db4o.Port"));

		public void Start(TextWriter writer)
		{
			_server = Db4oClientServer.OpenServer(File, Port);
			_server.GrantAccess(User, Password);
			_server.Ext().Configure().ExceptionsOnNotStorable(true);
			_server.Ext().Configure().MessageLevel(3);
			_server.Ext().Configure().SetOut(writer);
			_server.Ext().Configure().ActivationDepth(25);
		}

		public void Stop()
		{
			if (_server != null)
				_server.Close();
		}

		#region IDisposable Members

		void IDisposable.Dispose()
		{
			Stop();
		}

		#endregion
	}


	public class DeliveryStore
	{
		internal IObjectContainer Connection;
	}
}
