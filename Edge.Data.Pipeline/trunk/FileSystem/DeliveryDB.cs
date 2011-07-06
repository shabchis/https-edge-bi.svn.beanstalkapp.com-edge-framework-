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

			previousActivationDepth = client.Ext().Configure().ActivationDepth();
			int resultCount = -1;

			try
			{
				// set activation depth to 0 so that only an object reference is retrieved, this can then be used to swap the object
				if (!activate)
					client.Ext().Configure().ActivationDepth(0);

				var results = (from Delivery d in client where d.DeliveryID == deliveryID select d);
				resultCount = results.Count();
				delivery = resultCount > 0 ? results.First() : null;
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
/*
#if DEBUG
			Log.Write(String.Format("Delivery - {3}found: {0} (activate: {2}, {1} results)\n", deliveryID, resultCount, activate, delivery == null ? "not " : "" ), LogMessageType.Information);

#endif			
*/
			return delivery;
		}

		internal static Guid Save(Delivery delivery)
		{
			Log.Write(String.Format("Delivery - saving: {0}\n", delivery.DeliveryID), LogMessageType.Information);
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

		internal static Delivery[] GetSimilars(Delivery exampleDelivery, int activationLevel = -1)
		{
			Delivery[] similars;
			using (var client = DeliveryDBClient.Connect())
			{
				IObjectSet results = client.QueryByExample(exampleDelivery);
				similars = new Delivery[results.Count];
				results.CopyTo(similars, 0);
			}
			return similars;
		}

		internal static void Rollback(Delivery[] deliveries)
		{
			string cmdText = AppSettings.Get(typeof(Delivery), Consts.AppSettings.Delivery_RollbackCommand);
			SqlCommand cmd = DataManager.CreateCommand(cmdText, System.Data.CommandType.StoredProcedure);

			using (SqlConnection connection = new SqlConnection(AppSettings.GetConnectionString(typeof(Delivery), Consts.AppSettings.Delivery_SqlDb)))
			{
				connection.Open();
				using (SqlTransaction transaction = connection.BeginTransaction())
				{
					cmd.Connection = connection;
					cmd.Transaction = transaction;

					foreach (Delivery delivery in deliveries)
					{
						string guid = delivery.DeliveryID.ToString("N");
						DeliveryHistoryEntry commitEntry = delivery.History.Last(entry => entry.Operation == DeliveryOperation.Comitted);
						if (commitEntry == null)
							throw new Exception(String.Format("The delivery '{0}' has never been comitted so it cannot be rolled back.", guid));

						cmd.Parameters["@DeliveryID"].Value = guid;
						cmd.Parameters["@TableName"].Value = commitEntry.Parameters[Consts.DeliveryHistoryParameters.TablePerfix];

						cmd.ExecuteNonQuery();
					}

					transaction.Commit();
				}
			}

			// If all the rollback transactions executed successfully, add a rolled back entry to all of them
			foreach (Delivery delivery in deliveries)
			{
				delivery.History.Add(
					DeliveryOperation.RolledBack,
					Service.Current != null ? new long?(Service.Current.Instance.InstanceID) : null);
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

		public static IObjectContainer Connect()
		{
			return Db4oClientServer.OpenClient(Host, Port, User, Password);
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
