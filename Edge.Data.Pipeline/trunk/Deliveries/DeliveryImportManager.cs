using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Pipeline.Importing
{
	public abstract class DeliveryImportManager
	{
		long _serviceInstanceID;

		public DeliveryImportManager(long serviceInstanceID)
		{
			this.State = DeliveryImportManagerState.Idle;
			_serviceInstanceID = serviceInstanceID;
		}
		

		public DeliveryImportManagerState State
		{
			get;
			private set;
		}

		public Delivery CurrentDelivery
		{
			get;
			private set;
		}

		public Dictionary<string, object> HistoryEntryParameters
		{
			get;
			private set;
		}

		public void BeginImport(Delivery delivery)
		{
			ThrowIfNotIdle();
			this.State = DeliveryImportManagerState.Importing;
			this.CurrentDelivery = delivery;
			this.HistoryEntryParameters = new Dictionary<string, object>();
			
			OnBeginImport();
		}

		public void EndImport()
		{
			if (this.State != DeliveryImportManagerState.Importing)
				throw new InvalidOperationException("EndImport can only be called after BeginImport.");
			OnEndImport();

			this.CurrentDelivery.History.Add(DeliveryOperation.Imported, _serviceInstanceID, this.HistoryEntryParameters);
			this.CurrentDelivery.Save();

			this.CurrentDelivery = null;
			this.HistoryEntryParameters = null;

			this.State = DeliveryImportManagerState.Idle;
		}

		public void Commit(Delivery[] deliveries)
		{
			ThrowIfNotIdle();
			this.State = DeliveryImportManagerState.Comitting;
			Dictionary<string, object>[] entryParams = new Dictionary<string, object>[deliveries.Length];
			OnBeginCommit();
			for(int i = 0; i < deliveries.Length; i++)
			{
				this.CurrentDelivery = deliveries[i];
				this.HistoryEntryParameters = new Dictionary<string, object>();

				OnCommit();
				entryParams[i] = this.HistoryEntryParameters;
			}

			this.CurrentDelivery = null;
			this.HistoryEntryParameters = null;

			OnEndCommit();

			// Add history and save
			for (int i = 0; i < deliveries.Length; i++)
			{
				Delivery delivery = deliveries[i];
				delivery.History.Add(DeliveryOperation.Comitted, this._serviceInstanceID, entryParams[i]);
				delivery.Save();
			}
			
			this.State = DeliveryImportManagerState.Idle;
		}

		public void Rollback(Delivery[] deliveries)
		{
			ThrowIfNotIdle();
			this.State = DeliveryImportManagerState.Comitting;
			Dictionary<string, object>[] entryParams = new Dictionary<string, object>[deliveries.Length];
			OnBeginRollback();
			for (int i = 0; i < deliveries.Length; i++)
			{
				this.CurrentDelivery = deliveries[i];
				this.HistoryEntryParameters = new Dictionary<string, object>();

				OnRollback();
				entryParams[i] = this.HistoryEntryParameters;
			}

			this.CurrentDelivery = null;
			this.HistoryEntryParameters = null;

			OnEndRollback();

			// Add history and save
			for (int i = 0; i < deliveries.Length; i++)
			{
				Delivery delivery = deliveries[i];
				delivery.History.Add(DeliveryOperation.Comitted, this._serviceInstanceID, entryParams[i]);
				delivery.Save();
			}

			this.State = DeliveryImportManagerState.Idle;
		}

		void ThrowIfNotIdle()
		{
			if (this.State != DeliveryImportManagerState.Idle)
				throw new InvalidOperationException("DeliveryImportManager is currently in a busy state.");
		}

		protected abstract void OnBeginImport();
		protected abstract void OnEndImport();
		protected abstract void OnBeginCommit();
		protected abstract void OnCommit();
		protected abstract void OnEndCommit();
		protected abstract void OnBeginRollback();
		protected abstract void OnRollback();
		protected abstract void OnEndRollback();
	}

	public enum DeliveryImportManagerState
	{
		Idle,
		Importing,
		Comitting,
		RollingBack
	}

}
