using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Core;
using Edge.Data.Pipeline;
using Edge.Core.Data;
using System.Data;
using Edge.Data.Pipeline.Objects;

namespace Edge.Data.Pipeline.Deliveries
{
	public class Delivery
	{
		DeliveryFileList _files;
		DateTimeRange _targetPeriod;
		DateTime _dateCreated = DateTime.Now;
		DateTime _dateModified = DateTime.Now;
		Dictionary<string, object> _parameters;
		DeliveryHistory<DeliveryOperation> _history;

		/// <summary>
		/// Creates a new delivery and sets the specified instance ID as the creator.
		/// </summary>
		public Delivery(long instanceID)
		{
			this.History.Add(DeliveryOperation.Created, instanceID);
		}

		/// <summary>
		/// Gets the unique ID of the delivery (-1 if unsaved).
		/// </summary>
		public int DeliveryID
		{
			get { throw new NotImplementedException(); }
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
				if (this.DeliveryID > 0)
					throw new InvalidOperationException("This property can only be set before the first Save() is called.");

				_targetPeriod = value;
			}
		}

		/// <summary>
		/// Gets the files of the delivery.
		/// </summary>
		public DeliveryFileList Files
		{
			get { return _files ?? (_files = new DeliveryFileList()); }
		}

		/// <summary>
		/// Gets general parameters for use by services processing this delivery.
		/// </summary>
        public Dictionary<string,object> Parameters
		{
			get { return _parameters ?? (_parameters = new Dictionary<string, object>()); }
		}

		/// <summary>
		/// Represents the history of operations on the delivery. Each service that does an operation related to this delivery
		/// should add itself with the corresponding action.
		/// </summary>
		public DeliveryHistory<DeliveryOperation> History
		{
			get { return _history ?? (_history = new DeliveryHistory<DeliveryOperation>()); }
		}

		public void Save()
		{
			throw new NotImplementedException();
			
			if (Saved != null)
				Saved(this);
		}

		internal event Action<Delivery> Saved;
	}
    
	
	public enum DeliveryOperation
	{
		Created = 1,
		Retrieved = 2,
		Processed = 3,
		RolledBack = 4
	}
    
}
