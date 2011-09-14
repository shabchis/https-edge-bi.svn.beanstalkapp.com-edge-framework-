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
using System.Xml.Serialization;
using System.Xml;



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
			Delivery delivery = null;
			bool innerConnection = connection == null;

			if (innerConnection)
				connection = DeliveryDBClient.Connect();

			try
			{
				SqlCommand cmd = DataManager.CreateCommand("Delivery_Get(@deliveryID:Char, @deep:bit)", System.Data.CommandType.StoredProcedure);
				cmd.Connection = connection;
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
								DeliveryFile deliveryFile = delivery.Files[reader["FileID"].ToString()];
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
				SqlTransaction transaction = client.BeginTransaction();
				Guid guid = delivery.DeliveryID;

				if (guid != Guid.Empty)
				{
					SqlCommand cmd = new SqlCommand("Delivery_Delete");

					cmd.Connection = client;
					cmd.Transaction = transaction;
					cmd.CommandType = System.Data.CommandType.StoredProcedure;
					cmd.Parameters.Add("@deliveryID", System.Data.SqlDbType.Char);
					cmd.Parameters["@deliveryID"].Value = delivery.DeliveryID.ToString("N");
					cmd.ExecuteNonQuery();


					//**********************Delivery*********************************
					cmd = new SqlCommand(@"INSERT INTO [Edge_System].[dbo].[Delivery]
	       ([DeliveryID]
	       ,[AccountID]
	       ,[ChannelID]
	       ,[DateCreated]
	       ,[DateModified]
	       ,[Signature]
	       ,[Description]
	       ,[TargetLocationDirectory]
	       ,[TargetPeriodDefinition]
	       ,[TargetPeriodStart]
	       ,[TargetPeriodEnd])
	 VALUES
	       (@deliveryID,
	        @accountID,
	        @channelID,
	        @dateCreated,
	        @dateModified,
	        @signature,
	        @description,
	        @targetLocationDirectory,
	        @targetPeriodDefinition,
	        @targetPeriodStart,
	        @targetPeriodEnd)", client, transaction);

					cmd.Connection = client;
					cmd.Parameters.Add("@deliveryID", System.Data.SqlDbType.Char);
					cmd.Parameters.Add("@accountID", System.Data.SqlDbType.Int);
					cmd.Parameters.Add("@channelID", System.Data.SqlDbType.Int);
					cmd.Parameters.Add("@dateCreated", System.Data.SqlDbType.DateTime);
					cmd.Parameters.Add("@dateModified", System.Data.SqlDbType.DateTime);
					cmd.Parameters.Add("@signature", System.Data.SqlDbType.NVarChar);
					cmd.Parameters.Add("@description", System.Data.SqlDbType.NVarChar);
					cmd.Parameters.Add("@targetLocationDirectory", System.Data.SqlDbType.NVarChar);
					cmd.Parameters.Add("@targetPeriodDefinition", System.Data.SqlDbType.NVarChar);
					cmd.Parameters.Add("@targetPeriodStart", System.Data.SqlDbType.DateTime);
					cmd.Parameters.Add("@targetPeriodEnd", System.Data.SqlDbType.DateTime);

					cmd.Parameters["@deliveryID"].Value = delivery.DeliveryID.ToString("N");
					cmd.Parameters["@accountID"].Value = delivery.Account.ID;
					cmd.Parameters["@channelID"].Value = delivery.Channel.ID;
					cmd.Parameters["@dateCreated"].Value = delivery.DateCreated;
					cmd.Parameters["@dateModified"].Value = delivery.DateModified;
					cmd.Parameters["@signature"].Value = delivery.Signature;
					cmd.Parameters["@description"].Value = delivery.Description == null ? (object)DBNull.Value : delivery.Description;
					cmd.Parameters["@targetLocationDirectory"].Value = delivery.TargetLocationDirectory;
					cmd.Parameters["@targetPeriodDefinition"].Value = delivery.TargetPeriod.ToString();
					cmd.Parameters["@targetPeriodStart"].Value = delivery.TargetPeriodStart;
					cmd.Parameters["@targetPeriodEnd"].Value = delivery.TargetPeriodEnd;

					cmd.ExecuteNonQuery();

					//*******************DeliveryParameters***********************
					
					foreach (KeyValuePair<string, object> param in delivery.Parameters)
					{
						
						cmd = new SqlCommand(@"INSERT INTO [Edge_System].[dbo].[DeliveryParameters]
										([DeliveryID]
										,[Key]
										,[Value])
										 VALUES
									    (@deliveryID
									    ,@key
										,@value)");
						cmd.Connection = client;
						cmd.Transaction = transaction;
						cmd.Parameters.Add("@deliveryID", System.Data.SqlDbType.Char);
						cmd.Parameters.Add("@key", System.Data.SqlDbType.NVarChar);
						cmd.Parameters.Add("@value", System.Data.SqlDbType.Xml);
						cmd.Parameters["@deliveryID"].Value = delivery.DeliveryID.ToString("N");
						cmd.Parameters["@key"].Value = param.Key;
						XmlSerializer x =new  XmlSerializer(param.Value.GetType());
						MemoryStream s = new MemoryStream();
						x.Serialize(s,param.Value);
						cmd.Parameters["@value"].Value = new System.Data.SqlTypes.SqlXml(s);
						cmd.ExecuteNonQuery();

					}
					//*******************DeliveryHistory***********************
					foreach (DeliveryHistoryEntry historyEntry in delivery.History)
					{
						cmd = new SqlCommand(@"INSERT INTO [Edge_System].[dbo].[DeliveryParameters]
										([DeliveryID]
										,[Key]
										,[Value])
										 VALUES
									    (@deliveryID
									    ,@key
										,@value)");
						cmd.Connection = client;
						cmd.Transaction = transaction;
						
					}
					                      

					transaction.Commit();



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

		internal static void Delete(Delivery delivery)
		{
			using (SqlConnection connection = DeliveryDBClient.Connect())
			{
				using (SqlCommand cmd = new SqlCommand("Delivery_Delete"))
				{
					cmd.Connection = connection;
					cmd.CommandType = System.Data.CommandType.StoredProcedure;
					cmd.Parameters.Add("@deliveryID", System.Data.SqlDbType.Char);
					cmd.Parameters["@deliveryID"].Value = delivery.DeliveryID.ToString("N");
					cmd.ExecuteNonQuery();

				}
			}
		}

		internal static Delivery[] GetBySignature(string signature, Guid[] exclude)
		{
			List<Delivery> deliveries = new List<Delivery>();
			using (var client = DeliveryDBClient.Connect())
			{
				// Select deliveries that match a signature but none of the guids in 'exclude'
				using (SqlCommand cmd = DataManager.CreateCommand("Delivery_GetBySignature(@signature:NvarChar,@exclude:NvarChar)", System.Data.CommandType.StoredProcedure))
				{
					cmd.Connection = client;
					cmd.Parameters["@signature"].Value = "'" + signature + "'";
					cmd.Parameters["@exclude"].Value = GetGuidStringArray(exclude); //TODO: Talk with doron it's not the way to do it
					using (SqlDataReader reader = cmd.ExecuteReader())//TODO: Talk with doron it's not the way to do it
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
			StringBuilder guidArray = new StringBuilder();
			foreach (Guid guid in exclude)
				guidArray.AppendFormat(guidArray.Length == 0 ? "'{0}'" : ",'{0}'", guid.ToString("N"));

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
		public static SqlConnection Connect()
		{
			SqlConnection connection = new SqlConnection(AppSettings.GetConnectionString(typeof(Delivery), "DB"));
			connection.Open();
			return connection;
		}
	}





}
