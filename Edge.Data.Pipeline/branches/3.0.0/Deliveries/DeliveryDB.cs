using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Core.Configuration;
using Edge.Core.Utilities;
using Edge.Core;
using System.Diagnostics;
using System.IO;
using System.Data.Common;
using System.Data.SqlClient;
using Edge.Core.Services;
using Edge.Data.Objects;
using System.Xml.Serialization;
using System.Xml;
using System.Data.SqlTypes;
using System.Runtime.Serialization;
using System.Collections;
using Newtonsoft.Json;
using System.Data;




namespace Edge.Data.Pipeline
{
	internal class DeliveryDB
	{
		internal class Const
		{
			public const string SP_DeliveryGet = "SP.DeliveryGet";
			public const string SP_DeliveryDelete = "SP.DeliveryDelete";
			public const string SP_OutputDelete = "SP.OutputDelete";
		}

		internal static Delivery GetDelivery(Guid deliveryID, bool deep = true, SqlConnection connection = null)
		{
			Delivery delivery = null;
			bool innerConnection = connection == null;

			if (innerConnection)
				connection = DeliveryDBClient.Connect();

			try
			{
				SqlCommand cmd = SqlUtility.CreateCommand(String.Format("{0}(@deliveryID:Char, @deep:bit)", AppSettings.Get(typeof(DeliveryDB), Const.SP_DeliveryGet)), System.Data.CommandType.StoredProcedure);
				cmd.Connection = connection;
				cmd.Parameters["@deliveryID"].Value = deliveryID.ToString("N");
				cmd.Parameters["@deep"].Value = deep;

				using (SqlDataReader reader = cmd.ExecuteReader())
				{

					while (reader.Read())
					{
						#region Delivery
						// ..................

						delivery = new Delivery(reader.Convert<string, Guid>("DeliveryID", s => Guid.Parse(s)))
						{
							FullyLoaded = deep,
							Account = reader.Convert<int?, Account>("Account_ID", id => id.HasValue ? new Account() { ID = id.Value } : null),
							Channel = reader.Convert<int?, Channel>("ChannelID", id => id.HasValue ? new Channel() { ID = id.Value } : null),
							DateCreated = reader.Get<DateTime>("DateCreated"),
							DateModified = reader.Get<DateTime>("DateModified"),
							Description = reader.Get<string>("Description"),
							FileDirectory = reader.Get<string>("FileDirectory"),
						};

						delivery.InternalSetTimePeriod(
							DateTimeRange.Parse(reader.Get<string>("TimePeriodDefinition")),
							reader.Get<DateTime>("TimePeriodStart"),
							reader.Get<DateTime>("TimePeriodEnd")
						);

						// ..................
						#endregion

						if (deep)
						{
							#region DeliveryParameters
							// ..................

							if (reader.NextResult())
							{
								while (reader.Read())
								{

									delivery.Parameters.Add(reader.Get<string>("Key"), DeserializeJson(reader.Get<string>("Value")));
								}
							}
							// ..................
							#endregion

							#region DeliveryFile
							// ..................

							if (reader.NextResult())
							{
								while (reader.Read())
								{
									DeliveryFile deliveryFile = new DeliveryFile();
									deliveryFile.FileID = reader.Convert<string, Guid>("DeliveryID", s => Guid.Parse(s));
									deliveryFile.FileCompression = reader.Get<FileCompression>("FileCompression");
									deliveryFile.SourceUrl = reader.Get<string>("SourceUrl");
									deliveryFile.Name = reader.Get<string>("Name");
									deliveryFile.Location = reader.Get<string>("Location");
									deliveryFile.Status = reader.Get<DeliveryFileStatus>("Status");
									deliveryFile.FileSignature = reader.Get<string>("FileSignature");
									delivery.Files.Add(deliveryFile);
								}

							}
							// ..................
							#endregion

							#region DeliveryFileParameters
							// ..................

							if (reader.NextResult())
							{
								while (reader.Read())
								{

									DeliveryFile deliveryFile = delivery.Files[reader.Get<string>("Name")];
									deliveryFile.Parameters.Add(reader.Get<string>("Key"), DeserializeJson(reader.Get<string>("Value")));
								}

							}

							// ..................
							#endregion

							#region DeliveryOutput

							if (reader.NextResult())
							{
								while (reader.Read())
								{
									var deliveryOutput = new DeliveryOutput()
									{

										OutputID = reader.Convert<string, Guid>("OutputID", s => Guid.Parse(s)),
										Account = reader.Convert<int?, Account>("AccountID", id => id.HasValue ? new Account() {  ID = id.Value } : null),
										Channel = reader.Convert<int?, Channel>("ChannelID", id => id.HasValue ? new Channel() { ID = id.Value } : null),
										Signature = reader.Get<string>("Signature"),
										Status = reader.Get<DeliveryOutputStatus>("Status"),
										TimePeriodStart = reader.Get<DateTime>("TimePeriodStart"),
										TimePeriodEnd = reader.Get<DateTime>("TimePeriodEnd"),
										PipelineInstanceID = reader.Get<Guid>("PipelineInstanceID")
									};
									delivery.Outputs.Add(deliveryOutput);
								}

							}

							#endregion

							#region DeliveryOutputParameters
							// ..................

							if (reader.NextResult())
							{
								while (reader.Read())
								{

									DeliveryOutput deliveryOutput = delivery.Outputs[reader.Get<string>("OutputID")];
									deliveryOutput.Parameters.Add(reader.Get<string>("Key"), DeserializeJson(reader.Get<string>("Value")));
								}

							}

							// ..................
							#endregion

							#region checksum
							// ..................

							if (reader.NextResult())
							{
								while (reader.Read())
								{
									var deliveryOutput = delivery.Outputs[reader.Get<string>("OutputID")];
									if (deliveryOutput.Checksum == null)
										deliveryOutput.Checksum = new Dictionary<string, double>();
									deliveryOutput.Checksum.Add(reader["MeasureName"].ToString(), Convert.ToDouble(reader["Total"]));



								}

							}
							// ..................
							#endregion


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

			return delivery;
		}

		internal static Guid SaveDelivery(Delivery delivery)
		{
			if (!delivery.FullyLoaded)
				throw new InvalidOperationException("Cannot save a delivery that was loaded with deep = false.");

			using (var client = DeliveryDBClient.Connect())
			{
				SqlTransaction transaction = client.BeginTransaction();
				Guid guid = delivery.DeliveryID;

				if (guid != Guid.Empty)
				{
					#region [Delete]
					// ..................

					SqlCommand cmd = SqlUtility.CreateCommand((String.Format("{0}(@deliveryID:Char)", AppSettings.Get(typeof(DeliveryDB), Const.SP_DeliveryDelete))), System.Data.CommandType.StoredProcedure);

					cmd.Connection = client;
					cmd.Transaction = transaction;
					cmd.CommandType = System.Data.CommandType.StoredProcedure;
					cmd.Parameters["@deliveryID"].Value = delivery.DeliveryID.ToString("N");
					cmd.ExecuteNonQuery();

					// ..................
					#endregion

					#region Delivery
					// ..................
					if (delivery.DateCreated == DateTime.MinValue)
						delivery.DateCreated = DateTime.Now;
					delivery.DateModified = DateTime.Now;

					cmd = SqlUtility.CreateCommand(@"
						INSERT INTO [Delivery] (
								[DeliveryID],
								[Account_ID],
								[Account_OriginalID],
								[ChannelID],
								[DateCreated],
								[DateModified],
								[Description],
								[FileDirectory],
								[TimePeriodDefinition],
								[TimePeriodStart],
								[TimePeriodEnd]
						)
						VALUES (
								@deliveryID:Char,
								@account_ID:Int,
								@account_OriginalID:NVarChar,
								@channelID:Int,
								@dateCreated:DateTime,
								@dateModified:DateTime,
								@description:NVarChar,
								@fileDirectory:NVarChar,
								@timePeriodDefinition:NVarChar,
								@timePeriodStart:DateTime2,
								@timePeriodEnd:DateTime2
						)
						", System.Data.CommandType.Text);

					cmd.Connection = client;
					cmd.Transaction = transaction;

					cmd.Parameters["@deliveryID"].Value = delivery.DeliveryID.ToString("N");
					cmd.Parameters["@account_ID"].Value = delivery.Account != null ? delivery.Account.ID : -1;
					cmd.Parameters["@channelID"].Value = delivery.Channel != null ? delivery.Channel.ID : -1; ;
					cmd.Parameters["@dateCreated"].Value = delivery.DateCreated;
					cmd.Parameters["@dateModified"].Value = delivery.DateModified;
					cmd.Parameters["@description"].Value = delivery.Description == null ? (object)DBNull.Value : delivery.Description;
					cmd.Parameters["@fileDirectory"].Value = delivery.FileDirectory;
					cmd.Parameters["@timePeriodDefinition"].Value = delivery.TimePeriodDefinition.ToString();
					cmd.Parameters["@timePeriodStart"].Value = delivery.TimePeriodStart;
					cmd.Parameters["@timePeriodEnd"].Value = delivery.TimePeriodEnd;
					cmd.ExecuteNonQuery();

					// ..................
					#endregion

					#region DeliveryParameters
					// ..................

					foreach (KeyValuePair<string, object> param in delivery.Parameters)
					{

						cmd = SqlUtility.CreateCommand(@"
							INSERT INTO [DeliveryParameters](
								[DeliveryID],
								[Key],
								[Value]
							)
							VALUES (
								@deliveryID:Char,
								@key:NVarChar,
								@value:NVarChar
							)
							", System.Data.CommandType.Text);
						cmd.Connection = client;
						cmd.Transaction = transaction;
						cmd.Parameters["@deliveryID"].Value = delivery.DeliveryID.ToString("N");
						cmd.Parameters["@key"].Value = param.Key;
						cmd.Parameters["@value"].Value = Serialize(param.Value);
						cmd.ExecuteNonQuery();
					}

					// ..................
					#endregion

					#region DeliveryFile
					// ..................

					foreach (DeliveryFile file in delivery.Files)
					{
						if (file.FileID == Guid.Empty)
							file.FileID = Guid.NewGuid();
						if (file.DateCreated == DateTime.MinValue)
							file.DateCreated = DateTime.Now;
						file.DateModified = DateTime.Now;

						cmd = SqlUtility.CreateCommand(@"
							INSERT INTO [DeliveryFile] (
								[DeliveryID],
								[FileID],
								[Name],
								[DateCreated],
								[DateModified],
								[FileCompression],
								[SourceUrl],
								[Location],
								[Status],
								[FileSignature]
							)
							VALUES (
								@deliveryID:Char,
								@fileID:Char,
								@name:NVarChar,
								@dateCreated:DateTime,
								@dateModified:DateTime,
								@fileCompression:Int,
								@sourceUrl:NVarChar,
								@location:NVarChar,
								@status:Int,
								@fileSignature:NVarChar
							)", System.Data.CommandType.Text);
						cmd.Connection = client;
						cmd.Transaction = transaction;

						cmd.Parameters["@deliveryID"].Value = file.Delivery.DeliveryID.ToString("N");
						cmd.Parameters["@fileID"].Value = file.FileID.ToString("N");
						cmd.Parameters["@name"].Value = file.Name;
						cmd.Parameters["@dateCreated"].Value = file.DateCreated;
						cmd.Parameters["@dateModified"].Value = file.DateModified;
						cmd.Parameters["@fileCompression"].Value = file.FileCompression;
						cmd.Parameters["@sourceUrl"].Value = file.SourceUrl == null ? (object)DBNull.Value : file.SourceUrl;
						cmd.Parameters["@location"].Value = file.Location == null ? (object)DBNull.Value : file.Location;
						cmd.Parameters["@status"].Value = file.Status;
                        cmd.Parameters["@fileSignature"].Value = file.FileSignature==null ? (object)DBNull.Value : file.FileSignature;

						cmd.ExecuteNonQuery();
					}

					// ..................
					#endregion

					#region DeliveryFileParameters
					// ..................

					foreach (DeliveryFile file in delivery.Files)
					{
						foreach (KeyValuePair<string, object> param in file.Parameters)
						{
							cmd = SqlUtility.CreateCommand(@"
								INSERT INTO [DeliveryFileParameters] (
									[DeliveryID],
									[Name],
									[Key],
									[Value]
								)
								VALUES (
									@deliveryID:Char,
									@name:NVarChar,
									@key:NVarChar,
									@value:NVarChar
								)", System.Data.CommandType.Text);
							cmd.Connection = client;
							cmd.Transaction = transaction;

							cmd.Parameters["@deliveryID"].Value = delivery.DeliveryID.ToString("N");
							cmd.Parameters["@name"].Value = file.Name;
							cmd.Parameters["@key"].Value = param.Key;
							cmd.Parameters["@value"].Value = Serialize(param.Value);
							cmd.ExecuteNonQuery();
						}
					}

					// ..................
					#endregion

					#region DeliveryOutput
					// ..................

					foreach (DeliveryOutput output in delivery.Outputs)
					{
						if (output.OutputID == Guid.Empty)
							output.OutputID = Guid.NewGuid();
						if (output.DateCreated == DateTime.MinValue)
							output.DateCreated = DateTime.Now;
						output.DateModified = DateTime.Now;

						cmd = SqlUtility.CreateCommand(@"
							INSERT INTO [DeliveryOutput] (
								[DeliveryID],
								[OutputID],
								[AccountID],
								[AccountOriginalID],
								[ChannelID],
								[Signature],
								[Status] ,								
								[TimePeriodStart],
								[TimePeriodEnd],
								[DateCreated],
								[DateModified],
								[PipelineInstanceID]
							)
							VALUES (
								@deliveryID:Char,
								@outputID:Char,
								@accountID:Int,
								@accountOriginalID:NVarChar,
								@channelID:Int,
								@signature:NVarChar,
								@status:Int,								
								@timePeriodStart:DateTime2,
								@timePeriodEnd:DateTime2,
								@dateCreated:DateTime,
								@dateModified:DateTime,
								@pipelineInstanceID:BigInt
							)", System.Data.CommandType.Text);
						cmd.Connection = client;
						cmd.Transaction = transaction;

						cmd.Parameters["@deliveryID"].Value = output.Delivery.DeliveryID.ToString("N");
						cmd.Parameters["@outputID"].Value = output.OutputID.ToString("N");
						cmd.Parameters["@accountID"].Value = output.Account != null ? output.Account.ID : -1;
						// TODO shirat - Original ID
						//cmd.Parameters["@accountOriginalID"].Value = output.Account != null ? output.Account.OriginalID != null ? output.Account.OriginalID : (object)DBNull.Value : (object)DBNull.Value;
						cmd.Parameters["@channelID"].Value = output.Channel != null ? output.Channel.ID : -1;
						cmd.Parameters["@signature"].Value = output.Signature;
						cmd.Parameters["@status"].Value = output.Status;
						cmd.Parameters["@timePeriodStart"].Value = output.TimePeriodStart;
						cmd.Parameters["@timePeriodEnd"].Value = output.TimePeriodEnd;
						cmd.Parameters["@dateCreated"].Value = output.DateCreated;
						cmd.Parameters["@dateModified"].Value = output.DateModified;
						cmd.Parameters["@pipelineInstanceID"].Value = output.PipelineInstanceID.HasValue ? output.PipelineInstanceID.Value : (object)DBNull.Value;

						cmd.ExecuteNonQuery();
					}

					// ..................
					#endregion

					#region DeliveryOutputParameters
					// ..................

					foreach (DeliveryOutput output in delivery.Outputs)
					{
						foreach (KeyValuePair<string, object> param in output.Parameters)
						{
							cmd = SqlUtility.CreateCommand(@"
								INSERT INTO [DeliveryOutputParameters] (
									[DeliveryID],
									[OutputID],
									[Key],
									[Value]
								)
								VALUES (
									@deliveryID:Char,
									@outputID:NVarChar,
									@key:NVarChar,
									@value:NVarChar
								)", System.Data.CommandType.Text);
							cmd.Connection = client;
							cmd.Transaction = transaction;

							cmd.Parameters["@deliveryID"].Value = delivery.DeliveryID.ToString("N");
							cmd.Parameters["@outputID"].Value = output.OutputID.ToString("N");
							cmd.Parameters["@key"].Value = param.Key;
							cmd.Parameters["@value"].Value = Serialize(param.Value);
							cmd.ExecuteNonQuery();
						}
					}

					// ..................
					#endregion

					#region checkSum
					foreach (var output in delivery.Outputs)
					{
						foreach (KeyValuePair<string, double> sum in output.Checksum)
						{
							cmd = SqlUtility.CreateCommand(@"
							INSERT INTO [DeliveryOutputChecksum] (
								[DeliveryID],
								[OutputID],
								[MeasureName],
								[Total]
							)
							VALUES (
								@deliveryID:Char,
								@outputid:Char,
								@measureName:NVarChar,
								@total:decimal
								
							)", System.Data.CommandType.Text);
							cmd.Connection = client;
							cmd.Transaction = transaction;

							cmd.Parameters["@deliveryID"].Value = delivery.DeliveryID.ToString("N");
							cmd.Parameters["@outputid"].Value = output.OutputID.ToString("N");
							cmd.Parameters["@measureName"].Value = sum.Key;
							cmd.Parameters["@total"].Value = sum.Value;


							cmd.ExecuteNonQuery();


						}

					}
					#endregion



					transaction.Commit();

				}
				else
				{
					throw new NotSupportedException("In Pipeline 2.9, you cannot save a Delivery without first giving it a GUID.");
				}

				return guid;
			}
		}

		internal static Guid SaveOutput(DeliveryOutput output)
		{
			Guid guid = output.OutputID;

			using (var client = DeliveryDBClient.Connect())
			{
				SqlTransaction transaction = client.BeginTransaction();


				if (guid != Guid.Empty)
				{
					#region [Delete]
					// ..................

					SqlCommand cmd = SqlUtility.CreateCommand((String.Format("{0}(@outputID:Char)", AppSettings.Get(typeof(DeliveryDB), Const.SP_OutputDelete))), System.Data.CommandType.StoredProcedure);

					cmd.Connection = client;
					cmd.Transaction = transaction;
					cmd.CommandType = System.Data.CommandType.StoredProcedure;
					cmd.Parameters["@outputID"].Value = output.OutputID.ToString("N");
					cmd.ExecuteNonQuery();

					// ..................
					#endregion









					#region DeliveryOutput
					// ..................


					if (output.OutputID == Guid.Empty)
						output.OutputID = Guid.NewGuid();
					if (output.DateCreated == DateTime.MinValue)
						output.DateCreated = DateTime.Now;
					output.DateModified = DateTime.Now;

					cmd = SqlUtility.CreateCommand(@"
							INSERT INTO [DeliveryOutput] (
								[DeliveryID],
								[OutputID],
								[AccountID],
								[AccountOriginalID],
								[ChannelID],
								[Signature],
								[Status] ,								
								[TimePeriodStart],
								[TimePeriodEnd],
								[DateCreated],
								[DateModified],
								[PipelineInstanceID]
							)
							VALUES (
								@deliveryID:Char,
								@outputID:Char,
								@accountID:Int,
								@accountOriginalID::NVarChar,
								@channelID:Int,
								@signature:NVarChar,
								@status:Int,								
								@timePeriodStart:DateTime2,
								@timePeriodEnd:DateTime2,
								@dateCreated:DateTime,
								@dateModified:DateTime,
								@pipelineInstanceID:BigInt
							)", System.Data.CommandType.Text);
					cmd.Connection = client;
					cmd.Transaction = transaction;

					 cmd.Parameters["@deliveryID"].Value = output.Delivery.DeliveryID;
					cmd.Parameters["@outputID"].Value = output.OutputID.ToString("N");
					cmd.Parameters["@accountID"].Value = output.Account != null ? output.Account.ID : -1;
					cmd.Parameters["@channelID"].Value = output.Channel != null ? output.Channel.ID : -1;
					cmd.Parameters["@signature"].Value = output.Signature;
					cmd.Parameters["@status"].Value = output.Status;
					cmd.Parameters["@timePeriodStart"].Value = output.TimePeriodStart;
					cmd.Parameters["@timePeriodEnd"].Value = output.TimePeriodEnd;
					cmd.Parameters["@dateCreated"].Value = output.DateCreated;
					cmd.Parameters["@dateModified"].Value = output.DateModified;
					cmd.Parameters["@pipelineInstanceID"].Value = output.PipelineInstanceID.HasValue ? output.PipelineInstanceID.Value : (object)DBNull.Value;

					cmd.ExecuteNonQuery();


					// ..................
					#endregion

					#region DeliveryOutputParameters
					// ..................


					foreach (KeyValuePair<string, object> param in output.Parameters)
					{
						cmd = SqlUtility.CreateCommand(@"
								INSERT INTO [DeliveryOutputParameters] (
									[DeliveryID],
									[OutputID],
									[Key],
									[Value]
								)
								VALUES (
									@deliveryID:Char,
									@outputID:NVarChar,
									@key:NVarChar,
									@value:NVarChar
								)", System.Data.CommandType.Text);
						cmd.Connection = client;
						cmd.Transaction = transaction;

						cmd.Parameters["@deliveryID"].Value = output.Delivery.DeliveryID.ToString("N");
						cmd.Parameters["@outputID"].Value = output.OutputID.ToString("N");
						cmd.Parameters["@key"].Value = param.Key;
						cmd.Parameters["@value"].Value = Serialize(param.Value);
						cmd.ExecuteNonQuery();
					}


					// ..................
					#endregion

					#region checkSum

					foreach (KeyValuePair<string, double> sum in output.Checksum)
					{
						cmd = SqlUtility.CreateCommand(@"
							INSERT INTO [DeliveryOutputChecksum] (
								[DeliveryID],
								[OutputID],
								[MeasureName],
								[Total]
							)
							VALUES (
								@deliveryID:Char,
								@outputid:Char,
								@measureName:NVarChar,
								@total:decimal
								
							)", System.Data.CommandType.Text);
						cmd.Connection = client;
						cmd.Transaction = transaction;

						cmd.Parameters["@deliveryID"].Value = output.Delivery.DeliveryID.ToString("N");
						cmd.Parameters["@outputid"].Value = output.OutputID.ToString("N");
						cmd.Parameters["@measureName"].Value = sum.Key;
						cmd.Parameters["@total"].Value = sum.Value;


						cmd.ExecuteNonQuery();


					}


					#endregion



					transaction.Commit();

				}
				else
				{
					throw new NotSupportedException("In Pipeline 2.9, you cannot save a Delivery without first giving it a GUID.");
				}
			}

			return guid;

		}

		internal static void Delete(Delivery delivery)
		{
			using (SqlConnection connection = DeliveryDBClient.Connect())
			{
				using (SqlCommand cmd = new SqlCommand(AppSettings.Get(typeof(DeliveryDB), Const.SP_DeliveryDelete)))
				{
					cmd.Connection = connection;
					cmd.CommandType = System.Data.CommandType.StoredProcedure;
					cmd.Parameters.Add("@deliveryID", System.Data.SqlDbType.Char);
					cmd.Parameters["@deliveryID"].Value = delivery.DeliveryID.ToString("N");
					cmd.ExecuteNonQuery();

				}
			}
		}

		internal static Delivery[] GetDeliveriesBySignature(string signature, Guid exclude)
		{
			List<Delivery> deliveries = new List<Delivery>();
			using (var client = DeliveryDBClient.Connect())
			{
				// Select deliveries that match a signature but none of the guids in 'exclude'
				using (SqlCommand cmd = SqlUtility.CreateCommand("Delivery_GetBySignature(@signature:NvarChar,@exclude:NvarChar)", System.Data.CommandType.StoredProcedure))
				{
					cmd.Connection = client;
					cmd.Parameters["@signature"].Value = signature;
					cmd.Parameters["@exclude"].Value = exclude.ToString("N");
					using (SqlDataReader reader = cmd.ExecuteReader())
					{
						while (reader.Read())
							deliveries.Add(GetDelivery(Guid.Parse(reader.GetString(0))));
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

		public static Delivery[] GetDeliveriesByTargetPeriod(int channelID, int accountID, DateTime start, DateTime end, bool exact)
		{
			List<Delivery> deliveries = new List<Delivery>();
			List<string> deliveriesId = new List<string>();

			using (var client = DeliveryDBClient.Connect())
			{
				using (SqlCommand cmd = SqlUtility.CreateCommand("Delivery_GetByTargetPeriod(@channelID:Int,@accountID:Int,@targetPeriodStart:DateTime2,@targetPeriodEnd:DateTime2,@exact:bit)", System.Data.CommandType.StoredProcedure))
				{
					cmd.Connection = client;
					cmd.Parameters["@channelID"].Value = channelID;
					cmd.Parameters["@accountID"].Value = accountID;
					cmd.Parameters["@targetPeriodStart"].Value = start;
					cmd.Parameters["@targetPeriodEnd"].Value = end;
					cmd.Parameters["@exact"].Value = exact;

					using (SqlDataReader reader = cmd.ExecuteReader())
					{
						while (reader.Read())
							//deliveriesId.Add(Get(Guid.Parse(reader.GetString(0))));
							deliveriesId.Add(reader.GetString(0));
					}
				}
				foreach (string id in deliveriesId)
				{
					deliveries.Add(GetDelivery(Guid.Parse(id)));
				}
				return deliveries.ToArray();
			}


		}
		public static DeliveryOutput[] GetOutputsByTargetPeriod(int channelID, int accountID, DateTime start, DateTime end)
		{
			List<DeliveryOutput> outputs = new List<DeliveryOutput>();
			List<string> outputsIds = new List<string>();

			using (var client = DeliveryDBClient.Connect())
			{
				using (SqlCommand cmd = SqlUtility.CreateCommand("Output_GetByTargetPeriod(@channelID:Int,@accountID:Int,@targetPeriodStart:DateTime2,@targetPeriodEnd:DateTime2)", System.Data.CommandType.StoredProcedure))
				{
					cmd.Connection = client;
					cmd.Parameters["@channelID"].Value = channelID;
					cmd.Parameters["@accountID"].Value = accountID;
					cmd.Parameters["@targetPeriodStart"].Value = start;
					cmd.Parameters["@targetPeriodEnd"].Value = end;

					using (SqlDataReader reader = cmd.ExecuteReader())
					{
						while (reader.Read())
							//deliveriesId.Add(Get(Guid.Parse(reader.GetString(0))));
							outputsIds.Add(reader.GetString(0));
					}
				}
				foreach (string id in outputsIds)
				{
					outputs.Add(GetOutput(Guid.Parse(id)));
				}
				return outputs.ToArray();
			}


		}

		static bool? _ignoreJsonErrors = null;
		public static bool IgnoreJsonErrors
		{
			get
			{
				if (_ignoreJsonErrors == null || !_ignoreJsonErrors.HasValue)
				{
					
					if (Service.Current != null && Service.Current.Configuration.Parameters.Get<bool>("IgnoreDeliveryJsonErrors", false))
						_ignoreJsonErrors = true;
					else
						_ignoreJsonErrors = false;
				}
				return _ignoreJsonErrors.Value;
			}
		}

		private static object DeserializeJson(string json)
		{
			object toReturn = null;
			JsonSerializerSettings s = new JsonSerializerSettings();
			s.TypeNameHandling = TypeNameHandling.All;
			s.TypeNameAssemblyFormat = System.Runtime.Serialization.Formatters.FormatterAssemblyStyle.Full;


			if (!IgnoreJsonErrors)
			{
				toReturn = JsonConvert.DeserializeObject(json, s);
			}
			else
			{
				try { toReturn = JsonConvert.DeserializeObject(json, s); }
				catch (Exception ex)
				{
					// WARNING: this will just ignore the param value. If the delivery is saved later, this param value will be deleted from the database.
					// This is okay usually because this happens during rollback, and after rollback - nobody cares anymore about these old values.

					Log.Write("DeliveryDB", "Error while deserializing delivery parameter JSON.", ex);
					toReturn = null;
				}
			}

			return toReturn;


		}
		private static string Serialize(object param)
		{
			JsonSerializerSettings s = new JsonSerializerSettings();
			s.TypeNameHandling = TypeNameHandling.All;
			s.TypeNameAssemblyFormat = System.Runtime.Serialization.Formatters.FormatterAssemblyStyle.Full;

			return JsonConvert.SerializeObject(param, Newtonsoft.Json.Formatting.None, s);
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


		internal static DeliveryOutput[] GetOutputsBySignature(string signature, Guid exclude)
		{
			List<DeliveryOutput> outputs = new List<DeliveryOutput>();
			using (var client = DeliveryDBClient.Connect())
			{
				// Select deliveries that match a signature but none of the guids in 'exclude'
				using (SqlCommand cmd = SqlUtility.CreateCommand("OutPut_GetBySignature(@signature:NvarChar,@exclude:NvarChar)", System.Data.CommandType.StoredProcedure))
				{
					cmd.Connection = client;
					cmd.Parameters["@signature"].Value = signature;
					cmd.Parameters["@exclude"].Value = exclude.ToString("N");
					using (SqlDataReader reader = cmd.ExecuteReader())
					{
						while (reader.Read())
							outputs.Add(GetOutput(Guid.Parse(reader.GetString(0))));
					}
				}
			}
			return outputs.ToArray();
		}

		public static DeliveryOutput GetOutput(Guid guid)
		{
			DeliveryOutput output = null;
			using (var client = DeliveryDBClient.Connect())
			{
				// Select deliveries that match a signature but none of the guids in 'exclude'
				using (SqlCommand cmd = SqlUtility.CreateCommand("OutPut_Get(@outputID:Char)", System.Data.CommandType.StoredProcedure))
				{

					cmd.Connection = client;
					cmd.Parameters["@outputID"].Value = guid.ToString("N");

					using (SqlDataReader reader = cmd.ExecuteReader())
					{

						while (reader.Read())
						{
							#region DeliveryOutput
							output = new DeliveryOutput()
							{
								DeliveryID=reader.Convert<string, Guid>("DeliveryID", s => Guid.Parse(s)),
								OutputID = reader.Convert<string, Guid>("OutputID", s => Guid.Parse(s)),
								Account = reader.Convert<int?, Account>("AccountID", id => id.HasValue ? new Account() { ID = id.Value } : null),
								Channel = reader.Convert<int?, Channel>("ChannelID", id => id.HasValue ? new Channel() { ID = id.Value } : null),
								Signature = reader.Get<string>("Signature"),
								Status = reader.Get<DeliveryOutputStatus>("Status"),
								TimePeriodStart = reader.Get<DateTime>("TimePeriodStart"),
								TimePeriodEnd = reader.Get<DateTime>("TimePeriodEnd"),
								PipelineInstanceID = reader.Get<Guid?>("PipelineInstanceID")
							};

							#endregion

							#region DeliveryOutputParameters
							// ..................

							if (reader.NextResult())
							{
								while (reader.Read())
								{


									output.Parameters.Add(reader.Get<string>("Key"), DeserializeJson(reader.Get<string>("Value")));
								}

							}

							// ..................
							#endregion

							#region checksum
							// ..................

							if (reader.NextResult())
							{
								while (reader.Read())
								{

									if (output.Checksum == null)
										output.Checksum = new Dictionary<string, double>();
									output.Checksum.Add(reader["MeasureName"].ToString(), Convert.ToDouble(reader["Total"]));



								}

							}
							// ..................
							#endregion


						}

					}
				}
			}
			return output;


		}
	}
}

