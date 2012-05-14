using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Core.Services;
using Edge.Data.Pipeline.Configuration;
using Edge.Data.Pipeline;
using System.Text.RegularExpressions;
using System.Configuration;
using Edge.Core.Configuration;
using System.Threading;
using System.Data.SqlClient;
using EdgeConfiguration = Edge.Core.Data;
using Edge.Data.Pipeline.Mapping;

namespace Edge.Data.Pipeline.Services
{
	public abstract class PipelineService: Service
	{
		#region Core methods
		// ==============================

		protected sealed override void OnInit()
		{
			// TODO: check for required configuration options
		}

		protected sealed override ServiceOutcome DoWork()
		{
			ServiceOutcome outcome = DoPipelineWork();
			return outcome;
		}

		protected abstract ServiceOutcome DoPipelineWork();

		protected override void OnEnded(ServiceOutcome outcome)
		{
			// TODO: update delivery history automatically?
		}

		// ==============================
		#endregion

		#region Configuration
		// ==============================

		public static class ConfigurationOptionNames
		{
			public const string DeliveryID = "DeliveryID";
			public const string TargetPeriod = "TargetPeriod";
			public const string ConflictBehavior = "ConflictBehavior";
		}


		DateTimeRange? _range = null;
		public DateTimeRange TargetPeriod
		{
			get
			{
				if (_range == null)
				{
					if (Instance.Configuration.Options.ContainsKey(ConfigurationOptionNames.TargetPeriod))
					{
						_range = DateTimeRange.Parse(Instance.Configuration.Options[ConfigurationOptionNames.TargetPeriod]);
					}
					else
					{
						_range = DateTimeRange.AllOfYesterday; 
					}

					// enforce limitation
					if (this.TargetPeriodLimitation != DateTimeRangeLimitation.None)
					{
						DateTime start = this.TargetPeriod.Start.ToDateTime();
						DateTime end = this.TargetPeriod.End.ToDateTime();
						switch (this.TargetPeriodLimitation)
						{
							case DateTimeRangeLimitation.SameDay:
								if (end.Date != start.Date)
									throw new Exception("The specified range must be within the same day.");
								break;
							case DateTimeRangeLimitation.SameMonth:
								if (end.Month != start.Month || end.Year != start.Year)
									throw new Exception("The specified range must be within the same month.");
								break;
						}
					}
				}

				return _range.Value;
			}
		}

		protected virtual DateTimeRangeLimitation TargetPeriodLimitation
		{
			get { return DateTimeRangeLimitation.SameDay; }
		}

		Delivery _delivery = null;
		public Delivery Delivery
		{
			get
			{
				if (_delivery != null)
					return _delivery;

				Guid deliveryID = this.TargetDeliveryID;
				if (deliveryID != Guid.Empty)
					_delivery = DeliveryDB.Get(deliveryID);

				return _delivery;
			}
			set
			{
				if (this.Delivery != null)
					throw new InvalidOperationException("Cannot apply a delivery because a delivery is already applied.");

				_delivery = value;
			}
		}

		internal Guid TargetDeliveryID
		{
			get
			{
				Guid deliveryID;
				string did;
				if (!Instance.Configuration.Options.TryGetValue(ConfigurationOptionNames.DeliveryID, out did))
					return Guid.Empty;

				if (!Guid.TryParse(did, out deliveryID))
					throw new FormatException(String.Format("'{0}' is not a valid delivery GUID.", did));

				return deliveryID;
			}
		}

		public Delivery NewDelivery()
		{
			var d = new Delivery(this.TargetDeliveryID);
			d.History.Add(DeliveryOperation.Initialized, this.Instance.InstanceID);
			return d;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="delivery"></param>
		/// <param name="conflictBehavior">Indicates how conflicting deliveries will be handled.</param>
		/// <param name="importManager">The import manager that will be used to handle conflicting deliveries.</param>
		public DeliveryRollbackOperation HandleConflicts(DeliveryImportManager importManager, DeliveryConflictBehavior defaultConflictBehavior, bool getBehaviorFromConfiguration = true)
		{
			if (this.Delivery.Signature == null)
				throw new InvalidOperationException("Cannot handle conflicts before a valid signature is given to the delivery.");

			// ================================
			// Ticket behavior
			DeliveryTicketBehavior ticketBehavior=DeliveryTicketBehavior.Abort;
			if (getBehaviorFromConfiguration)
			{
				string configuredTicketBehavior;
				if (Instance.Configuration.Options.TryGetValue("TicketBehavior", out configuredTicketBehavior))
					ticketBehavior = (DeliveryTicketBehavior)Enum.Parse(typeof(DeliveryTicketBehavior), configuredTicketBehavior);
			}



			//prevent duplicate data in case two services runing on the same time
			if (ticketBehavior==DeliveryTicketBehavior.Abort)
			{
				using (SqlConnection sqlConnection = new SqlConnection(AppSettings.GetConnectionString("Edge.Core.Services", "SystemDatabase")))
				{
					// @"DeliveryTicket_Get(@deliverySignature:Nvarchar, @deliveryID:Nvarchar, $workflowInstanceID:bigint)"
					SqlCommand command = EdgeConfiguration.DataManager.CreateCommand(string.Format("{0}({1})", AppSettings.Get(this.GetType(), "DeliveryTicket.SP"), "@deliverySignature:NvarChar,@deliveryID:NvarChar,@workflowInstanceID:bigint"), System.Data.CommandType.StoredProcedure);
					command.Parameters["@deliverySignature"].Value = Delivery.Signature;
					command.Parameters["@deliveryID"].Value = Delivery.DeliveryID.ToString("N");
					command.Parameters["@workflowInstanceID"].Value = Instance.ParentInstance.InstanceID;
					command.Parameters["@workflowInstanceID"].Direction = System.Data.ParameterDirection.InputOutput;

					sqlConnection.Open();
					command.Connection = sqlConnection;
					if (((DeliveryTicketStatus)command.ExecuteScalar()) == DeliveryTicketStatus.ClaimedByOther)
						throw new Exception(String.Format("The current delivery signature is currently claimed by service instance ID {0}.", command.Parameters["@workflowInstanceID"].Value));
				} 
			}

			// ================================
			// Conflict behavior

			DeliveryConflictBehavior behavior = defaultConflictBehavior;
			if (getBehaviorFromConfiguration)
			{
				string configuredBehavior;
				if (Instance.Configuration.Options.TryGetValue("ConflictBehavior", out configuredBehavior))
					behavior = (DeliveryConflictBehavior)Enum.Parse(typeof(DeliveryConflictBehavior), configuredBehavior);
			}

			if (behavior == DeliveryConflictBehavior.Ignore)
				return null;

			Delivery[] conflicting = this.Delivery.GetConflicting();
			Delivery[] toCheck = new Delivery[conflicting.Length + 1];
			toCheck[0] = this.Delivery;
			conflicting.CopyTo(toCheck, 1);

			// Check whether the last commit was not rolled back for each conflicting delivery
			List<Delivery> toRollback = new List<Delivery>();
			foreach (Delivery d in toCheck)
			{
				int rollbackIndex = -1;
				int commitIndex = -1;
				for (int i = 0; i < d.History.Count; i++)
				{
					if (d.History[i].Operation == DeliveryOperation.Committed)
						commitIndex = i;
					else if (d.History[i].Operation == DeliveryOperation.RolledBack)
						rollbackIndex = i;
				}

				if (commitIndex > rollbackIndex)
					toRollback.Add(d);
			}

			DeliveryRollbackOperation operation = null;
			if (toRollback.Count > 0)
			{
				if (behavior == DeliveryConflictBehavior.Rollback)
				{
					operation = new DeliveryRollbackOperation();
					operation.AsyncDelegate = new Action<Delivery[]>(importManager.Rollback);
					operation.AsyncResult = operation.AsyncDelegate.BeginInvoke(toRollback.ToArray(), null, null);	
				}
				else
				{
					StringBuilder guids = new StringBuilder();
					for (int i = 0; i < toRollback.Count; i++)
					{
						guids.Append(toRollback[i].DeliveryID.ToString("N"));
						if (i < toRollback.Count - 1)
							guids.Append(", ");
					}
					throw new Exception("Conflicting deliveries found: " + guids.ToString());
				}
			}

			return operation;
		}

		// ==============================
		#endregion

		#region Mapping
		// ==============================

		MappingConfiguration _mapping = null;
		
		public MappingConfiguration Mappings
		{
			get
			{
				if (_mapping == null)
				{
					ConfigurationElement extension;
					if (!this.Instance.Configuration.Extensions.TryGetValue(MappingConfigurationElement.ExtensionName, out extension))
					{
						AccountElement account = EdgeServicesConfiguration.Current.Accounts.GetAccount(this.Instance.AccountID);
						if (!account.Extensions.TryGetValue(MappingConfigurationElement.ExtensionName, out extension))
							throw new MappingConfigurationException("No mapping configuration found.");
					}
					_mapping = ((MappingConfigurationElement) extension).Load();
				}

				return _mapping;
			}
		}

		// ==============================
		#endregion
	}

	public enum DeliveryConflictBehavior
	{
		Ignore,
		Abort,
		Rollback
	}

	public enum DeliveryTicketBehavior
	{
		Ignore,
		Abort
	}


	public enum DeliveryTicketStatus
	{
		ClaimedByOther = 0,
		AlreadyClaimed = 1,
		Claimed = 2
	}

	public enum DateTimeRangeLimitation
	{
		None,
		SameDay,
		SameMonth
	}

	public class DeliveryRollbackOperation
	{
		internal IAsyncResult AsyncResult;
		internal Action<Delivery[]> AsyncDelegate;

		public void Wait()
		{
			this.AsyncResult.AsyncWaitHandle.WaitOne();
			this.AsyncDelegate.EndInvoke(this.AsyncResult);
		}
	}

}
