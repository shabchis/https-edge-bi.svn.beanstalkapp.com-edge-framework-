﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Data.Objects;

namespace Edge.Data.Pipeline
{
	public class DeliveryOutput: IDeliveryChild
	{
		#region IDeliveryChild Members

		string IDeliveryChild.Key
		{
			get { return this.OutputID.ToString("N"); }
		}

		Delivery IDeliveryChild.Delivery
		{
			get
			{
				return this.Delivery;
			}
			set
			{
				this.Delivery = value;
			}
		}

		#endregion

		public DeliveryOutput()
		{
			Checksum = new Dictionary<string, double>();
			this.OutputID = Guid.NewGuid();
			
		}

		public void Save()
		{
			this.OutputID = DeliveryDB.SaveOutput(this);
		}
		Dictionary<string, object> _parameters;

		/// <summary>
		/// Gets the unique ID of the file;
		/// </summary>
		public Guid OutputID { get; internal set; }

		/// <summary>
		/// Gets the Delivery ID of the file;
		/// </summary>
		public Guid DeliveryID { get; internal set; }

		/// <summary>
		/// 
		/// </summary>
		public Delivery Delivery { get; set; }

		/// <summary>
		/// 
		/// </summary>
		public string Signature { get; set; }

		/// <summary>
		/// 
		/// </summary>
		public Account Account { get; set; }

		/// <summary>
		/// 
		/// </summary>
		public Channel Channel { get; set; }

		/// <summary>
		/// 
		/// </summary>
		public DateTime TimePeriodStart { get; set; }

		/// <summary>
		/// 
		/// </summary>
		public DateTime TimePeriodEnd { get; set; }

		/// <summary>
		/// 
		/// </summary>
		//public bool AllowIsolatedRollback { get; set; }

		/// <summary>
		/// 
		/// </summary>
		public DeliveryOutputStatus Status { get; set; }

		/// <summary>
		/// 
		/// </summary>
		public Guid? PipelineInstanceID { get; internal set; }

		/// <summary>
		/// Gets the date the delivery file was created.
		/// </summary>
		public DateTime DateCreated { get; internal set; }

		/// <summary>
		/// Gets the date the delivery file was last modified.
		/// </summary>
		public DateTime DateModified { get; internal set; }

		/// <summary>
		/// Gets general parameters for use by services processing this delivery file.
		/// </summary>
		public Dictionary<string, object> Parameters
		{
			get { return _parameters ?? (_parameters = new Dictionary<string, object>()); }
			set { _parameters = value; }
		}

		public Dictionary<string,double> Checksum { get; set; }

		/// <summary>
		/// Gets outputs that conflict with the current output by signature.
		/// </summary>
		/// <returns></returns>
		public DeliveryOutput[] GetConflicting()
		{
			DeliveryOutput[] conflicting = null;
			if (this.Signature == null)
				throw new InvalidOperationException("The output does not have a signature - cannot search for conflicts.");

			conflicting= DeliveryDB.GetOutputsBySignature(this.Signature, exclude: this.OutputID);
			return conflicting;
		}

		public static DeliveryOutput Get(Guid guid)
		{
			return DeliveryDB.GetOutput(guid);
		}

		public static DeliveryOutput[] GetByTimePeriod(DateTime timePeriodStart, DateTime timePeriodEnd, Channel channel, Account account)
		{
			return DeliveryDB.GetOutputsByTargetPeriod(channel.ID, account.ID, timePeriodStart, timePeriodEnd);
		}
	}
}
