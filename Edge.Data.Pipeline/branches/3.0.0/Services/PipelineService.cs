using System;
using System.Collections.Generic;
using System.Configuration;
using Edge.Core.Services;
using Edge.Data.Pipeline.Mapping;

namespace Edge.Data.Pipeline.Services
{
	public abstract class PipelineService : Service
	{
		#region Core methods
		// ==============================

		protected sealed override ServiceOutcome DoWork()
		{
			ServiceOutcome outcome = DoPipelineWork();
			return outcome;
		}

		protected abstract ServiceOutcome DoPipelineWork();

		// ==============================
		#endregion

		#region Configuration
		// ==============================

		public new PipelineServiceConfiguration Configuration
		{
			get { return (PipelineServiceConfiguration)base.Configuration; }
		}

		Delivery _delivery;
		public Delivery Delivery
		{
			get
			{
				if (_delivery != null)
					return _delivery;

				if (Configuration.DeliveryID != null)
					_delivery = DeliveryDB.GetDelivery(Configuration.DeliveryID.Value);

				return _delivery;
			}
			set
			{
				if (Delivery != null)
					throw new InvalidOperationException("Cannot apply a delivery because a delivery is already applied.");

				_delivery = value;
			}
		}

		public Delivery NewDelivery()
		{
			if (Configuration.DeliveryID == null)
				throw new ConfigurationErrorsException("Delivery ID cannot be null");

			if (Configuration.TimePeriod == null)
				throw new ConfigurationErrorsException(String.Format("Time period is not set for delivery '{0}'", Delivery.DeliveryID));

			return new Delivery(Configuration.DeliveryID.Value) {TimePeriodDefinition = Configuration.TimePeriod.Value};
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="importManager">The import manager that will be used to handle conflicting deliveries.</param>
		/// <param name="defaultConflictBehavior"></param>
		/// <param name="getBehaviorFromConfiguration"></param>
		public void HandleConflicts(DeliveryManager importManager, DeliveryConflictBehavior defaultConflictBehavior, bool getBehaviorFromConfiguration = true)
		{
			if (Delivery.Outputs.Count < 1)
				return;

			ServiceInstance parent = AsServiceInstance();
			while (parent.ParentInstance != null)			
				parent = parent.ParentInstance;

			foreach (DeliveryOutput output in Delivery.Outputs)
			{
				if (!output.PipelineInstanceID.HasValue)
					output.PipelineInstanceID = parent.InstanceID;
			}

			Delivery.Save();

			// ================================
			// Conflict behavior

			DeliveryConflictBehavior behavior = Configuration.ConflictBehavior != null ?
												Configuration.ConflictBehavior.Value :
												defaultConflictBehavior;

			var processing = new List<DeliveryOutput>();
			var committed = new List<DeliveryOutput>();
			foreach (DeliveryOutput output in Delivery.Outputs)
			{
				DeliveryOutput[] conflicts = output.GetConflicting();

				foreach (var conflict in conflicts)
				{
					if (conflict.PipelineInstanceID != null)
					{
						ServiceInstance instance = Environment.GetServiceInstance(conflict.PipelineInstanceID.Value, stateInfoOnly: true);
						if (instance.State != ServiceState.Ended)
							processing.Add(conflict);
					}

					if (conflict.Status == DeliveryOutputStatus.Committed || conflict.Status==DeliveryOutputStatus.Staged)
						committed.Add(conflict);
				}
			}
			if (processing.Count > 0)
			{
				foreach (var output in Delivery.Outputs)
					output.Status = DeliveryOutputStatus.Canceled;

				Delivery.Save();
				throw new DeliveryConflictException("There are outputs with the same signatures currently being processed:") { ConflictingOutputs = processing.ToArray() }; // add list of output ids
			}

			if (behavior == DeliveryConflictBehavior.Ignore)
				return;

			if (committed.Count > 0)
			{
				foreach (var output in Delivery.Outputs)
					output.Status = DeliveryOutputStatus.Canceled;

				Delivery.Save();
				throw new DeliveryConflictException("There are outputs with the same signatures are already committed\\staged:") { ConflictingOutputs = committed.ToArray() }; // add list of output ids
			}
		}

		protected override string GetLogContextInfo()
		{
			return string.Format("AccountID: {0}, DeliveryID: {1}", Delivery.Account.ID, Delivery.DeliveryID);
		}

		// ==============================
		#endregion

		#region Mapping
		// ==============================
		MappingConfiguration _mapping;

		public MappingConfiguration Mappings
		{
			get
			{
				if (_mapping == null)
				{
					_mapping = new MappingConfiguration();
					_mapping.Usings.Add("System.{0}, mscorlib");
					_mapping.Usings.Add("Edge.Data.Objects.{0}, Edge.Data.Objects");
					_mapping.Usings.Add("Edge.Data.Pipeline.Objects.{0}, Edge.Data.Pipeline");

					// TODO shirat - remove obsolute configuration, load mapping configuration from file, may be to move mappings to Processor
					//var extension = Configuration.Parameters.Get<MappingConfigurationElement>("MappingConfigurationElement", false);
					//if (extension != null)
					//	extension.LoadInto(_mapping);
					LoadMappings();
				}
				return _mapping;
			}
		}

		protected virtual void LoadMappings() {}
		// ==============================
		#endregion
	}

	public enum DeliveryConflictBehavior
	{
		Ignore,
		Abort
	}
}
