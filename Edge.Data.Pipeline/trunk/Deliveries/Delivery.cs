using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Core;
using Edge.Data.Pipeline;
using Edge.Core.Data;
using System.Data;
using Edge.Data.Objects;
using Db4objects.Db4o;

namespace Edge.Data.Pipeline
{
	public class Delivery
	{
		DeliveryFileList _files;
		DateTimeRange _targetPeriod;
		DateTime _dateCreated = DateTime.Now;
		DateTime _dateModified = DateTime.Now;
		Dictionary<string, object> _parameters;
		DeliveryHistory _history;

		/// <summary>
		/// Creates a new delivery and sets the specified instance ID as the creator.
		/// </summary>
		/// <param name="instanceID">ID of the service that is initializing the delivery. Equivalent to delivery.History.Add(DeliveryOperation.Created, serviceInstance.InstanceID)</param>
		internal Delivery(long instanceID, Guid specifiedDeliveryID)
		{
			if (specifiedDeliveryID == Guid.Empty)
				throw new ArgumentNullException("In current version (Pipeline 2.9) a delivery ID is required when creating a new delivery. "+
					"If in an initializer service, check that the workflow service is defined as Edge.Data.Pipeline.Services.PipelineWorkflowService.");

			// fuck db4o
			_files = new DeliveryFileList(this);
			_history = new DeliveryHistory();
			_parameters = new Dictionary<string, object>();
			
			this.DeliveryID = specifiedDeliveryID;

			this.History.Add(DeliveryOperation.Initialized, instanceID);
		}

		/// <summary>
		/// Gets the unique ID of the delivery (Guid.Empty if unsaved).
		/// </summary>
		public Guid DeliveryID
		{
			get;
			private set;
		}

		public Account Account
		{
			get;
			set;
		}

		/// <summary>
		/// Location to save files of deliveries of this type. Example "Google/AdWords"
		/// </summary>
		public string TargetLocationDirectory
		{
			get;
			set;
		}

		/// <summary>
		/// Gets or sets the channel for which this channel is relevant
		/// </summary>
		public Channel Channel
		{
			get; set;
		}

		/// <summary>
		/// Gets/sets a general description of the delivery.
		/// </summary>
		public string Description
		{
			get;
			set;
		}

		/// <summary>
		/// Gets the date the delivery was created.
		/// </summary>
		public DateTime DateCreated
		{
			get { return _dateCreated; }
		}

		/// <summary>
		/// Gets the date the delivery was last modified.
		/// </summary>
		public DateTime DateModified
		{
			get { return _dateModified; }
		}

		/// <summary>
		/// Gets or sets the target dates this delivery contains data for.
		/// </summary>
		public DateTimeRange TargetPeriod
		{
			get { return _targetPeriod; }

			set
			{
				//if (this.DeliveryID > 0)
				//	throw new InvalidOperationException("This property can only be set before the first Save() is called.");

				_targetPeriod = value;
			}
		}

		/// <summary>
		/// Gets the files of the delivery.
		/// </summary>
		public DeliveryFileList Files
		{
			get { return _files; }
		}

		/// <summary>
		/// Gets general parameters for use by services processing this delivery.
		/// </summary>
        public Dictionary<string,object> Parameters
		{
			get { return _parameters; }
		}

		/// <summary>
		/// Represents the history of operations on the delivery. Each service that does an operation related to this delivery
		/// should add itself with the corresponding action.
		/// </summary>
		public DeliveryHistory History
		{
			get { return _history; }
		}

		/// <summary>
		/// Gets or sets a unique signature that will be used to identify whether any conflicting deliveries exist.
		/// </summary>
		public string Signature
		{
			get;
			set;
		}

		public void Save()
		{
			this.DeliveryID = DeliveryDB.Save(this);
		}

		public void Delete()
		{
			DeliveryDB.Delete(this);
		}

		public Delivery[] GetConflicting()
		{
			DeliveryDB.GetConflicting(this);
		}

		// Statics
		// =============================

		public static Delivery Get(Guid deliveryID)
		{
			return DeliveryDB.Get(deliveryID);
		}
	}
    
	
	public enum DeliveryOperation
	{
		Initialized = 1,
		Retrieved = 2,
		Imported = 3,
		Comitted = 4,
		RolledBack = 5
	}


	internal class RollbackOperation
	{
		public IAsyncResult AsyncResult;
		public Action<Delivery[]> AsyncDelegate;

		public void Wait()
		{
			this.AsyncResult.AsyncWaitHandle.WaitOne();
			this.AsyncDelegate.EndInvoke(this.AsyncResult);
		}
	}
}
