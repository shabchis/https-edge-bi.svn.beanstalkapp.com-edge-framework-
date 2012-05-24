using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Core;
using Edge.Data.Pipeline;
using Edge.Core.Data;
using System.Data;
using Edge.Data.Objects;

namespace Edge.Data.Pipeline
{
	public class Delivery
	{
		#region Consts
		// =============================
		public class Consts
		{
			public static class ConnectionStrings
			{
				public const string SqlStagingDatabase = "Sql.DeliveriesDb";
			}
		}
		// =============================
		#endregion

		#region Fields
		// =============================

		DateTimeRange _timePeriod;

		// =============================
		#endregion

		#region Constructor
		// =============================

		/// <summary>
		/// Creates a new delivery with the specified ID.
		/// </summary>
		public Delivery(Guid specifiedDeliveryID)
		{
			if (specifiedDeliveryID == Guid.Empty)
				throw new ArgumentNullException("In current version (Pipeline 2.9) a delivery ID is required when creating a new delivery. " +
					"If this exception occured in an initializer service, check that the workflow service is defined as Edge.Data.Pipeline.Services.PipelineWorkflowService.");

			this.Files = new DeliveryChildList<DeliveryFile>(this);
			this.Outputs = new DeliveryChildList<DeliveryOutput>(this);
			this.Parameters = new Dictionary<string, object>();

			this.FullyLoaded = true;
			this.DeliveryID = specifiedDeliveryID;
		}
		
		// =============================
		#endregion
	
		#region Properties
		// =============================

		public bool FullyLoaded { get; internal set; }

		/// <summary>
		/// 
		/// </summary>
		public Account Account { get; set; }

		/// <summary>
		/// 
		/// </summary>
		public Channel Channel { get; set; }

		/// <summary>
		/// Gets the unique ID of the delivery (Guid.Empty if unsaved).
		/// </summary>
		public Guid DeliveryID { get; private set; }

		/// <summary>
		/// Location to save files of deliveries of this type. Example "Google/AdWords"
		/// </summary>
		public string FileDirectory { get; set; }

		/// <summary>
		/// Gets/sets a general description of the delivery.
		/// </summary>
		public string Description { get; set; }

		/// <summary>
		/// Gets the date the delivery was created.
		/// </summary>
		public DateTime DateCreated { get; internal set; }

		/// <summary>
		/// Gets the date the delivery was last modified.
		/// </summary>
		public DateTime DateModified { get; internal set; }

		/// <summary>
		/// Contains the files of the delivery.
		/// </summary>
		public DeliveryChildList<DeliveryFile> Files { get; private set; }

		/// <summary>
		/// 
		/// </summary>
		public DeliveryChildList<DeliveryOutput> Outputs { get; private set; }

		/// <summary>
		/// Gets general parameters for use by services processing this delivery.
		/// </summary>
		public Dictionary<string, object> Parameters { get; private set; }

		/// <summary>
		/// Gets or sets the target dates this delivery contains data for.
		/// </summary>
		public DateTimeRange TimePeriodDefinition { get; set; }

		/// <summary>
		/// Used to break a single time period into separate outputs.
		/// </summary>
		public DateTimeRangeSplitResolution TimePeriodSplit { get; set; }

		/// <summary>
		/// 
		/// </summary>
		public DateTime TimePeriodStart { get; private set; }

		/// <summary>
		/// 
		/// </summary>
		public DateTime TimePeriodEnd { get; private set; }

		// =============================
		#endregion

		#region Methods
		// =============================

		public void Save()
		{
			this.DeliveryID = DeliveryDB.Save(this);
		}

		public void Delete()
		{
			DeliveryDB.Delete(this);
		}

		internal void InternalSetTimePeriod(DateTimeRange range, DateTime calculatedStart, DateTime calculatedEnd)
		{
			_timePeriod = range;
			this.TimePeriodStart = calculatedStart;
			this.TimePeriodEnd = calculatedEnd;
		}
		
		
		// =============================
		#endregion


		#region Statics
		// =============================

		public static Delivery Get(Guid deliveryID, bool deep = true)
		{
			return DeliveryDB.Get(deliveryID, deep);
		}

		/*
		public static Delivery[] GetByTargetPeriod(DateTime start, DateTime end, Channel channel = null, Account account = null, bool exact = false)
		{
			return DeliveryDB.GetByTargetPeriod(
				channel == null ? -1 : channel.ID,
				account == null ? -1 : account.ID,
				start,
				end,
				exact);
		}
		*/

		public static string CreateSignature(string value)
		{
			byte[] toEncodeAsBytes = System.Text.UTF8Encoding.UTF8.GetBytes(value);
			string returnValue = System.Convert.ToBase64String(toEncodeAsBytes);

			return returnValue;

		}
		// =============================
		#endregion

	}
}
