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
		public Guid RequestID { get; private set; }
		public ServiceConfiguration Configuration { get; set; }
		public SchedulingRule Rule { get; private set; }
		public DateTime RequestedTime { get; private set; }
		public SchedulingRequest(ServiceConfiguration configuration, SchedulingRule rule, DateTime requestedTime)
		{
			this.Configuration = configuration;
			this.Rule = rule;
			this.RequestedTime = requestedTime;

			if (rule.Scope == SchedulingScope.Unplanned)
				this.RequestID = rule.GuidForUnplanned;
			else
				this.RequestID = Guid.NewGuid();
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
		internal volatile bool Activated = false;
		public string UniqueKey
		{
			get
			{
				return String.Format("profile:{0},base:{1},name:{2},scope:{3},time:{4}", this.Configuration.Profile.ID, Configuration.BaseConfiguration.Name, Configuration.Name, Rule.Scope, RequestedTime);
			}
		}

		private bool _saved = false;
		private object _saveLock = new object();
		public SchedulingStatus SchedulingStatus { get;  set; }
		

		public void Save()
		{
			lock (_saveLock)
			{
				using (SqlConnection conn = new SqlConnection(AppSettings.GetConnectionString("Edge.Core.Services", "SystemDatabase")))
				{
					SqlCommand command;
					if (!_saved)
					{
						command = DataManager.CreateCommand(@"INSERT INTO [Scheduling]
						   ([RequestID]
						   ,[UniqueKey]
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
						   @UniqueKey:nvarchar,
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
							[UniqueKey]=@UniqueKey:nvarchar
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
					command.Parameters["@UniqueKey"].Value = this.UniqueKey;
					command.Parameters["@RequestedTime"].Value = this.RequestedTime;
					command.Parameters["@InstanceName"].Value = this.Configuration.Name;
					command.Parameters["@InstanceUses"].Value = this.Configuration.BaseConfiguration.Name;
					command.Parameters["@LegacyInstanceID"].Value = this.Instance.LegacyInstance.InstanceID;
					command.Parameters["@Outcome"].Value = this.Instance.Outcome;
					command.Parameters["@RequestID"].Value = this.RequestID.ToString("N");
					command.Parameters["@SchedulingStatus"].Value = this.SchedulingStatus;
					command.Parameters["@SchedulingScope"].Value = this.Rule.Scope;
					command.Parameters["@ExpectedStartTime"].Value = this.Instance.ExpectedStartTime == DateTime.MinValue ? (object)DBNull.Value : this.Instance.ExpectedStartTime;
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
	}
	public enum SchedulingStatus
	{
		Request,
		Scheduled,
		Done,
		CouldNotBeScheduled
	}
}
