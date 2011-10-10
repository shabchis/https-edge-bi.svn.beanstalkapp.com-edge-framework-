using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Pipeline
{
	public abstract class DeliveryImportManager: IDisposable
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
			protected set;
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

		protected virtual int PreparePassCount
		{
			get { return 1; }
		}
		protected virtual int CommitPassCount
		{
			get { return 1; }
		}
		protected virtual int RollbackPassCount
		{
			get { return 1; }
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

			OnDisposeImport();
			OnDispose();
		}

		public void Prepare(Delivery[] deliveries)
		{
			this.Batch(deliveries,
				this.PreparePassCount,
				OnBeginPrepare,
				OnEndPrepare,
				OnBeginPreparePass,
				OnEndPreparePass,
				OnPrepare,
				OnDisposePrepare,
				DeliveryImportManagerState.Preparing,
				DeliveryOperation.Prepared);
		}

		public void Commit(Delivery[] deliveries)
		{
			this.Batch(deliveries,
				this.CommitPassCount,
				OnBeginCommit,
				OnEndCommit,
				OnBeginCommitPass,
				OnEndCommitPass,
				OnCommit,
				OnDisposeCommit,
				DeliveryImportManagerState.Comitting,
				DeliveryOperation.Committed);
		}

		public void Rollback(Delivery[] deliveries)
		{
			this.Batch(deliveries,
				this.RollbackPassCount,
				OnBeginRollback,
				OnEndRollback,
				OnBeginRollbackPass,
				OnEndRollbackPass,
				OnRollback,
				OnDisposeRollback,
				DeliveryImportManagerState.RollingBack,
				DeliveryOperation.RolledBack);
		}


		void Batch(Delivery[] deliveries,
			int passes,
			Action onBegin,
			Action<Exception> onEnd,
			Action<int> onBeginPass,
			Action<int> onEndPass,
			Action<int> onItem,
			Action onDispose,
			DeliveryImportManagerState activeState,
			DeliveryOperation historyOperation)
		{
			ThrowIfNotIdle();
			this.State = activeState;
			Dictionary<string, object>[] entryParams = new Dictionary<string, object>[deliveries.Length];

			onBegin();
			Exception exception = null;

			try
			{
				for (int pass = 0; pass < passes; pass++)
				{
					onBeginPass(pass);
					for (int i = 0; i < deliveries.Length; i++)
					{
						this.CurrentDelivery = deliveries[i];
						this.HistoryEntryParameters = entryParams[i] ?? (entryParams[i] = new Dictionary<string, object>());

						onItem(pass);
					}
					onEndPass(pass);
				}
			}
			catch (DeliveryConflictException dceex)
			{
				throw dceex;
			}
			catch (Exception ex)
			{
				exception = ex;
			}

			this.CurrentDelivery = null;
			this.HistoryEntryParameters = null;

			onEnd(exception);

			if (exception == null)
			{
				// Add history and save
				for (int i = 0; i < deliveries.Length; i++)
				{
					
					Delivery delivery = deliveries[i];
					delivery.History.Add(historyOperation, this._serviceInstanceID, entryParams[i]);
					delivery.Save();
				}
			}
			else
				// Throw exception
				throw new Exception("InnerException:", exception);

			this.State = DeliveryImportManagerState.Idle;

			onDispose();
			OnDispose();
		}

		void ThrowIfNotIdle()
		{
			if (this.State != DeliveryImportManagerState.Idle)
				throw new InvalidOperationException("DeliveryImportManager is currently in a busy state.");
		}

		public void Dispose()
		{
			OnDisposeImport();
			OnDisposeCommit();
			OnDisposeRollback();
			OnDispose();
		}

		protected virtual void OnBeginImport() {}
		protected virtual void OnEndImport() { }
		protected virtual void OnDisposeImport() { }
	
		protected virtual void OnBeginPrepare() { }
		protected virtual void OnBeginPreparePass(int pass) { }
		protected abstract void OnPrepare(int pass);
		protected virtual void OnEndPreparePass(int pass) { }
		protected virtual void OnEndPrepare(Exception ex) { }
		protected virtual void OnDisposePrepare() { }

		protected virtual void OnBeginCommit() { }
		protected virtual void OnBeginCommitPass(int pass) { }
		protected abstract void OnCommit(int pass);
		protected virtual void OnEndCommitPass(int pass) { }
		protected virtual void OnEndCommit(Exception ex) { }
		protected virtual void OnDisposeCommit() { }

		protected virtual void OnBeginRollback() { }
		protected virtual void OnBeginRollbackPass(int pass) { }
		protected abstract void OnRollback(int pass);
		protected virtual void OnEndRollbackPass(int pass) { }
		protected virtual void OnEndRollback(Exception ex) { }
		protected virtual void OnDisposeRollback() { }

		protected virtual void OnDispose() { }
	}

	public enum DeliveryImportManagerState
	{
		Idle,
		Importing,
		Preparing,
		Comitting,
		RollingBack
	}

}
