using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Data.SqlClient;
using Edge.Core.Configuration;
using Edge.Core.Data;

namespace Edge.Core.Scheduling.Objects
{
	public class SchedulingRequest
	{
		private bool _saved = false;
		private object _saveLock = new object();
		public Guid ParentRequestID { get; private set; }
		public Guid RequestID { get; private set; }
		public ServiceConfiguration Configuration { get; internal set; }
		public SchedulingRule Rule { get; private set; }
		public DateTime RequestedTime { get; private set; }
		public DateTime ScheduledStartTime { get; internal set; }
		public DateTime ScheduledEndTime { get; internal set; }
		
		public SchedulingStatus SchedulingStatus { get; set; }

		public event EventHandler Rescheduled;
		public event EventHandler Expired;

		public SchedulingRequest(ServiceConfiguration configuration, SchedulingRule rule, DateTime requestedTime)
		{
			this.Configuration = configuration;
			this.Rule = rule;
			this.RequestedTime = requestedTime;
			this.RequestID = Guid.NewGuid();
		}

		public SchedulingRequest(ServiceInstance instance, SchedulingRule rule, DateTime requestedTime): this(instance.Configuration, rule, requestedTime)
		{
		}

		public ServiceInstance Instance
		{
			get
			{
				if (this.Configuration is ServiceInstanceConfiguration)
					return ((ServiceInstanceConfiguration)Configuration).Instance;
				else
					return null;
			}
		}

		public string Signature
		{
			get
			{
				return String.Format("profile:{0},base:{1},name:{2},scope:{3},time:{4}", this.Configuration.Profile.ID, Configuration.BaseConfiguration.Name, Configuration.Name, Rule.Scope, RequestedTime);
			}
		}

		public TimeSpan ActualDeviation
		{
			get { return this.ScheduledStartTime.Subtract(this.RequestedTime); }
		}


		public void Save()
		{
			throw new NotImplementedException();

			lock (_saveLock)
			{
				using (SqlConnection conn = new SqlConnection(AppSettings.GetConnectionString("Edge.Core.Services", "SystemDatabase")))
				{
					SqlCommand command;
					if (!_saved)
					{
						command = DataManager.CreateCommand(@"INSERT INTO [Scheduling]
						   ([RequestID]
						   ,[Signature]
						   ,[RequestedTime]
						   ,[InstanceName]
						   ,[InstanceUses]
						   ,[LegacyInstanceID]
						   ,[Outcome]
						   ,[SchedulingScope]
						   ,[SchedulingStatus]
						   ,[ExpectedStartTime])
					 VALUES
						   (@RequestID:char,
						   @Signature:nvarchar,
						   @RequestedTime:datetime,
						   @InstanceName:nvarchar,
						   @InstanceUses:nvarchar,
						   @LegacyInstanceID:bigint,
						   @Outcome:int,
						   @SchedulingScope:int,
						   @SchedulingStatus:int,
						   @ExpectedStartTime:datetime)");
					}
					else
					{
						command = DataManager.CreateCommand(@"UPDATE [Scheduling]
						SET
							[Signature]=@Signature:nvarchar
						   ,[RequestedTime]=@RequestedTime:datetime
						   ,[InstanceName]=@InstanceName:nvarchar
						   ,[InstanceUses]=@InstanceUses:nvarchar
						   ,[LegacyInstanceID]=@LegacyInstanceID:bigint
						   ,[Outcome]=@Outcome:int
						   ,[SchedulingScope]=@SchedulingScope:int
						   ,[SchedulingStatus]=@SchedulingStatus:int
						   ,[ExpectedStartTime]=@ExpectedStartTime:datetime
							WHERE RequestID=@RequestID:char");
					}
					command.Parameters["@Signature"].Value = this.Signature;
					command.Parameters["@RequestedTime"].Value = this.RequestedTime;
					command.Parameters["@InstanceName"].Value = this.Configuration.Name;
					command.Parameters["@InstanceUses"].Value = this.Configuration.BaseConfiguration.Name;
					command.Parameters["@LegacyInstanceID"].Value = this.Instance.LegacyInstance.InstanceID;
					command.Parameters["@Outcome"].Value = this.Instance.Outcome;
					command.Parameters["@RequestID"].Value = this.RequestID.ToString("N");
					command.Parameters["@SchedulingStatus"].Value = this.SchedulingStatus;
					command.Parameters["@SchedulingScope"].Value = this.Rule.Scope;
					command.Parameters["@ExpectedStartTime"].Value = this.ScheduledStartTime == DateTime.MinValue ? (object)DBNull.Value : this.ScheduledStartTime;
					conn.Open();
					command.Connection = conn;
					if (command.ExecuteNonQuery() < 0)
						throw new Exception("Scheduling Request not saved");
					_saved = true;
				}
			}
		}


		/*
		public override string ToString()
		{
			string uniqueKey;
			if (Rule.Scope != SchedulingScope.Unplanned)
				uniqueKey = String.Format("profile:{0},base:{1},name:{2},scope:{3},time:{4}", this.Configuration.Profile.ID, Configuration.BaseConfiguration.Name, Configuration.Name, Rule.Scope, RequestedTime);
			else
				uniqueKey = Rule.GuidForUnplanned.ToString() ;

			return uniqueKey;
		}

		public override int GetHashCode()
		{
			int returnType = Rule.Scope != SchedulingScope.Unplanned ?
				this.ToString().GetHashCode() :
				Rule.GuidForUnplanned.GetHashCode();
			return returnType;
		}

		public override bool Equals(object obj)
		{
			if ((object)obj == null)
				return false;
			if (obj is SchedulingRequest)
				return obj.GetHashCode() == this.GetHashCode();
			else
				return false;
		}

		public static bool operator ==(SchedulingRequest sd1, SchedulingRequest sd2)
		{
			return sd1.Equals(sd2);
		}

		public static bool operator !=(SchedulingRequest sd1, SchedulingRequest sd2)
		{
			return !sd1.Equals(sd2);
		}
		*/

		public SchedulingRequestInfo GetInfo()
		{
			SchedulingRequestInfo requestInfo = new SchedulingRequestInfo();
			requestInfo.ActualStartTime = this.Instance.LegacyInstance.TimeStarted;
			requestInfo.ActualEndTime = this.Instance.LegacyInstance.TimeEnded;
			requestInfo.LegacyInstanceID = this.Instance.LegacyInstance.InstanceID;
			requestInfo.Options =this.Instance.LegacyInstance.Configuration.Options;
			requestInfo.ParentRequestID = this.ParentRequestID;
			requestInfo.ProfileID = Convert.ToInt32(this.Configuration.Profile.Settings["AccountID"]);
			requestInfo.Progress = this.Instance.Progress;
			requestInfo.RequestedTime = this.RequestedTime;
			requestInfo.RequestID = this.RequestID;
			requestInfo.ScheduledEndTime = this.ScheduledEndTime;
			requestInfo.ScheduledStartTime = this.ScheduledStartTime;
			requestInfo.SchedulingStatus = this.SchedulingStatus;
			requestInfo.ServiceName = this.Instance.Configuration.Name;
			requestInfo.ServiceOutcome = this.Instance.Outcome;
			requestInfo.ServiceState = this.Instance.State;
			return requestInfo;

		}
	}
	public enum SchedulingStatus
	{
		New = 0,
		Scheduled = 1,
		Activated = 2,
		Expired = 7,
		Canceled = 8
	}
}
