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
			Delivery delivery=null;
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

					while (reader.Read())
					{

						//**********************Delivery*********************************
						delivery = new Delivery(reader.Convert<string, Guid>("DeliveryID", s => Guid.Parse(s)));

						delivery.Account = reader.Convert<int?, Account>("AccountID", id => id.HasValue ? new Account() { ID = id.Value } : null);
						delivery.Channel = reader.Convert<int?, Channel>("ChannelID", id => id.HasValue ? new Channel() { ID = id.Value } : null);
						//delivery.DateCreated=DateTime.Parse(reader["DateCreated"].ToString());
						//delivery.DateModified=DateTime.Parse(reader["DateModified"].ToString());
						delivery.Signature = reader["Signature"].ToString();
						delivery.Description = reader["Description"].ToString();
						delivery.TargetLocationDirectory = reader["TargetLocationDirectory"].ToString();
						delivery.TargetPeriod = DateTimeRange.Parse(reader["TargetPeriodDefinition"].ToString());
						//delivery.TargetPeriodStart=DateTime.Parse(reader["TargetPeriodStart"].ToString());
						//delivery.TargetPeriodEnd=DateTime.Parse(reader["TargetPeriodEnd"].ToString());

						//*******************DeliveryParameters***********************

						if (reader.NextResult())
						{
							while (reader.Read())
								delivery.Parameters.Add(reader["Key"].ToString(), reader.Get<object>("Value"));
						}
						//*******************DeliveryHistory***********************
						if (reader.NextResult())
						{
							while (reader.Read())
								delivery.History.Add(new DeliveryHistoryEntry((DeliveryOperation)reader["Operation"], Convert.ToInt64(reader["ServiceInstanceID"])));							
						}

						//*******************DeliveryHistoryParameters***********************
						if (reader.NextResult())
						{
							while (reader.Read())
								delivery.History[reader.Get<int>("Index")].Parameters.Add(reader["Key"].ToString(), reader.Get<object>("Value"));
						}
						//*******************DeliveryFile***********************
						if (reader.NextResult())
						{
							while (reader.Read())
							{
								DeliveryFile deliveryFile = new DeliveryFile();
								deliveryFile.Account = reader.Convert<int?, Account>("AccountID", id => id.HasValue ? new Account() { ID = id.Value } : null);
								deliveryFile.FileID = reader.Convert<string, Guid>("DeliveryID", s => Guid.Parse(s));
								deliveryFile.FileFormat = (FileCompression)reader["FileCompression"];
								deliveryFile.SourceUrl = reader["SourceUrl"].ToString();
								deliveryFile.Name = reader["Name"].ToString();
								deliveryFile.Location = reader["Location"].ToString();
								delivery.Files.Add(deliveryFile);
							}

						}
						//*******************DeliveryFileParameters***********************
						if (reader.NextResult())
						{
							while (reader.Read())
							{
								DeliveryFile deliveryFile=delivery.Files[reader["FileID"].ToString()];
								deliveryFile.Parameters.Add(reader["Key"].ToString(), reader.Get<object>("Value"));
								
								
							}

						}
						//*******************DeliveryFileHistory***********************
						if (reader.NextResult())
						{
							while (reader.Read())
							{
								DeliveryFile deliveryFile = delivery.Files[reader["FileID"].ToString()];
								deliveryFile.History.Add(new DeliveryHistoryEntry((DeliveryOperation)reader["Operation"], Convert.ToInt64(reader["ServiceInstanceID"])));	
							}


						}
						//*******************DeliveryFileHistoryParameters***********************
						if (reader.NextResult())
						{
							while (reader.Read())
							{
								DeliveryFile deliveryFile = delivery.Files[reader["FileID"].ToString()];
								deliveryFile.History[reader.Get<int>("Index")].Parameters.Add(reader["Key"].ToString(), reader.Get<object>("Value"));
							}


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
						//long tempDb4oID = client.Ext().GetID(inactiveReference);
						//client.Ext().Bind(delivery, tempDb4oID);
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

				//client.Store(delivery);
				return guid;
			}
		}

		internal static void Delete(Delivery delivery, IObjectContainer client = null)
		{
			throw new NotImplementedException();
		}

		internal static Delivery[] GetBySignature(string signature, Guid[] exclude)
		{
			List<Delivery> deliveries = new List<Delivery>();
			using (var client = DeliveryDBClient.Connect())
			{
				// Select deliveries that match a signature but none of the guids in 'exclude'
				using (SqlCommand cmd=DataManager.CreateCommand("Delivery_GetBySignature(@signature:NvarChar,@exclude:NvarChar)",System.Data.CommandType.StoredProcedure))
				{
					cmd.Connection = client;
					cmd.Parameters["@signature"].Value = signature;
					cmd.Parameters["@exclude"].Value = GetGuidStringArray(exclude);
					using (SqlDataReader reader=cmd.ExecuteReader())
					{						
						while (reader.Read())						
							deliveries.Add(Get(Guid.Parse(reader.GetString(0))));
					}
				}
			}
			return deliveries.ToArray();
		}

		private static string GetGuidStringArray(Guid[] exclude)
		{
			StringBuilder guidArray=new StringBuilder();
			foreach (Guid guid in exclude)
				guidArray.AppendFormat(guidArray.Length == 0 ? "{0}" : ",{0}", guid.ToString("N"));

			return guidArray.ToString();
		}

		internal static Delivery[] GetByTargetPeriod(int channelID, int accountID, DateTime start, DateTime end)
		{
			List<Delivery> deliveries = new List<Delivery>();
			using (var client = DeliveryDBClient.Connect())
			{
				using (SqlCommand cmd = DataManager.CreateCommand("Delivery_GetByTargetPeriod(@channelID:Int,@accountID:Int,@targetPeriodStart:DateTime,@targetPeriodEnd:DateTime)", System.Data.CommandType.StoredProcedure))
				{
					cmd.Connection = client;
					cmd.Parameters["@channelID"].Value = channelID;
					cmd.Parameters["@accountID"].Value = accountID;
					cmd.Parameters["@targetPeriodStart"].Value = start;
					cmd.Parameters["@targetPeriodEnd"].Value = end;
					using (SqlDataReader reader = cmd.ExecuteReader())
					{
						while (reader.Read())
							deliveries.Add(Get(Guid.Parse(reader.GetString(0))));
					}
				}
			}
			return deliveries.ToArray();
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
