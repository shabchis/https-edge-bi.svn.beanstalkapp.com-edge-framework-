using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Data.Pipeline;
using Edge.Data.Pipeline.Services;
using Edge.Data.Objects;
using System.Data.SqlClient;
using Edge.Core.Configuration;
using Edge.Core.Data;

namespace Edge.Data.Pipeline.Metrics.Checksums
{
	abstract public class DeliveryDBChecksumBaseService : ValidationService
	{
		abstract protected ValidationResult DeliveryDbCompare(DeliveryOutput deliveryOutput, Dictionary<string, double> totals, string DbConnectionStringName, string comparisonTable);
		public static Double ALLOWED_DIFF = 0.1;
		public double progress = 0;

		protected override IEnumerable<ValidationResult> Validate()
		{
			Channel channel = new Channel();
			progress += 0.1;
			this.ReportProgress(progress);

			#region Getting Service option params
			//Getting Accounts list
			string[] accounts;
			if (this.Instance.AccountID == -1)
			{
				if (String.IsNullOrEmpty(this.Instance.Configuration.Options["AccountsList"]))
					throw new Exception("Missing Configuration option AccountsList");
				accounts = this.Instance.Configuration.Options["AccountsList"].Split(',');
			}
			else
			{
				List<string> account = new List<string>() { this.Instance.AccountID.ToString() };
				accounts = account.ToArray();
			}


			//Getting Table 
			string comparisonTable;
			if (String.IsNullOrEmpty(this.Instance.Configuration.Options["SourceTable"]))
				throw new Exception("Missing Configuration option SourceTable");
			else comparisonTable = this.Instance.Configuration.Options["SourceTable"];

			//Getting Channel List
			if (String.IsNullOrEmpty(this.Instance.Configuration.Options["ChannelList"]))
				throw new Exception("Missing Configuration option ChannelList");
			string[] channels = this.Instance.Configuration.Options["ChannelList"].Split(',');

			//Getting TimePeriod
			DateTime fromDate, toDate;
			if ((String.IsNullOrEmpty(this.Instance.Configuration.Options["fromDate"])) && (String.IsNullOrEmpty(this.Instance.Configuration.Options["toDate"])))
			{
				fromDate = this.TimePeriod.Start.ToDateTime();
				toDate = this.TimePeriod.End.ToDateTime();
			}
			else
			{
				fromDate = Convert.ToDateTime(this.Instance.Configuration.Options["fromDate"]);
				toDate = Convert.ToDateTime(this.Instance.Configuration.Options["toDate"]);
			}

			//Get Sub Channel (for cases such as Google Search and GDN)
			string subChannel = String.Empty;
			if (!String.IsNullOrEmpty(this.Instance.Configuration.Options["SubChannel"]))
			{
				subChannel = this.Instance.Configuration.Options["SubChannel"].ToString();
			}

			#endregion


			if (this.Delivery == null || this.Delivery.DeliveryID.Equals(Guid.Empty))
			{
				#region Creating Delivery Search List
				List<DeliveryOutput> deliveryOutputSearchList = new List<DeliveryOutput>();

				while (fromDate <= toDate)
				{
					// {start: {base : '2009-01-01', h:0}, end: {base: '2009-01-01', h:'*'}}
					var subRange = new DateTimeRange()
					{
						Start = new DateTimeSpecification()
						{
							BaseDateTime = fromDate,
							Hour = new DateTimeTransformation() { Type = DateTimeTransformationType.Exact, Value = 0 },
						},

						End = new DateTimeSpecification()
						{
							BaseDateTime = fromDate,
							Hour = new DateTimeTransformation() { Type = DateTimeTransformationType.Max },
						}
					};

					foreach (var Channel in channels)
					{
						foreach (string account in accounts)
						{
							DeliveryOutput deliveryOutput = new DeliveryOutput();
							deliveryOutput.Account = new Account() { ID = Convert.ToInt32(account) };
							deliveryOutput.Channel = new Channel() { ID = Convert.ToInt32(Channel) };
							deliveryOutput.TimePeriodStart = subRange.Start.ToDateTime();
							deliveryOutput.TimePeriodEnd = subRange.End.ToDateTime();

							deliveryOutputSearchList.Add(deliveryOutput);

							progress += 0.3 * ((1 - progress) / (channels.LongLength + accounts.LongLength));
							this.ReportProgress(progress);
						}
					}
					fromDate = fromDate.AddDays(1);
				}
				#endregion

				foreach (DeliveryOutput deliveryOutput in deliveryOutputSearchList)
				{
					//Getting criterion matched deliveries
					DeliveryOutput[] outputToCheck = DeliveryOutput.GetByTimePeriod(deliveryOutput.TimePeriodStart, deliveryOutput.TimePeriodEnd, deliveryOutput.Channel, deliveryOutput.Account);
					bool foundCommited = false;

					progress += 0.3 * (1 - progress);
					this.ReportProgress(progress);

					var outputToCheck_withSubChannel = new Object();
					if (!String.IsNullOrEmpty(subChannel))
					{
						 outputToCheck_withSubChannel = 
															from o in outputToCheck
															where o.Parameters.ContainsKey("SubChannel") && o.Parameters["SubChannel"].Equals(subChannel)
															select o;
					}


					foreach (DeliveryOutput output in outputToCheck)
					{
					//	if (output.Parameters.ContainsKey("SubChannel") && output.Parameters["SubChannel"].Equals(subChannel))
							if ((output.Status == DeliveryOutputStatus.Committed || output.Status == DeliveryOutputStatus.Staged) && output.Checksum != null && output.Checksum.Count > 0)
							{
								//Check Delivery data vs OLTP
								foundCommited = true;
								yield return (DeliveryDbCompare(output, output.Checksum, "OltpDB", comparisonTable));
							}

					}

					//could not find deliveries by user criterions
					if (outputToCheck.Length == 0)
					{
						yield return new ValidationResult()
						{
							ResultType = ValidationResultType.Error,
							AccountID = deliveryOutput.Account.ID,
							TargetPeriodStart = deliveryOutput.TimePeriodStart,
							TargetPeriodEnd = deliveryOutput.TimePeriodEnd,
							Message = "Cannot find outputs in DB",
							ChannelID = deliveryOutput.Channel.ID,
							CheckType = this.Instance.Configuration.Name
						};
					}
					else if (!foundCommited)
					{
						yield return new ValidationResult()
						{
							ResultType = ValidationResultType.Error,
							AccountID = deliveryOutput.Account.ID,
							TargetPeriodStart = deliveryOutput.TimePeriodStart,
							TargetPeriodEnd = deliveryOutput.TimePeriodEnd,
							Message = "Cannot find Commited output in DB",
							ChannelID = deliveryOutput.Channel.ID,
							CheckType = this.Instance.Configuration.Name
						};
					}

				} // End of foreach

			}
			else
			{
				//Getting current Delivery totals

				foreach (DeliveryOutput output in this.Delivery.Outputs)
				{
					if ((output.Status == DeliveryOutputStatus.Committed || output.Status == DeliveryOutputStatus.Staged) && output.Checksum != null && output.Checksum.Count > 0)
					{
						yield return (DeliveryDbCompare(output, output.Checksum, "OltpDB", comparisonTable));
					}
				}

			}
		}

	}

}
