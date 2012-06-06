using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Core.Utilities;

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

		protected virtual int TransformPassCount
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
			
			OnBeginImport();
		}

		public void EndImport()
		{
			if (this.State != DeliveryImportManagerState.Importing)
				throw new InvalidOperationException("EndImport can only be called after BeginImport.");
			OnEndImport();

			this.CurrentDelivery.Save();
			this.CurrentDelivery = null;

			this.State = DeliveryImportManagerState.Idle;

			OnDisposeImport();
			OnDispose();
		}

		public void Transform(Delivery[] deliveries)
		{
			this.Batch<Delivery>(deliveries,
				this.TransformPassCount,
				OnBeginTransform,
				ex =>
				{
					OnEndTransform(ex);

					if (ex == null)
					{
						foreach (Delivery d in deliveries)
						{
							foreach (DeliveryOutput output in d.Outputs)
							{
								output.Status = DeliveryOutputStatus.Transformed;
								
							}
							d.Save();
						}
							
					}
				},
				OnBeginTransformPass,
				OnEndTransformPass,
				OnTransform,
				DeliveryImportManagerState.Transforming);
		}

		public void Commit(Delivery[] deliveries)
		{
			this.Batch<Delivery>(deliveries,
				this.CommitPassCount,
				OnBeginCommit,
				ex =>
				{
					OnEndCommit(ex);

					if (ex == null)
					{
						foreach (Delivery d in deliveries)
							d.Save();
					}
				},
				OnBeginCommitPass,
				OnEndCommitPass,
				OnCommit,
				DeliveryImportManagerState.Comitting);
		}


		public void RollbackDeliveries(Delivery[] deliveries)
		{
			this.Batch<Delivery>(deliveries,
				this.RollbackPassCount,
				OnBeginRollback,
				ex =>
				{
					OnEndRollback(ex);

					if (ex == null)
					{
						foreach (Delivery d in deliveries)
							d.Save();
					}
				},
				OnBeginRollbackPass,
				OnEndRollbackPass,
				OnRollbackDelivery,
				DeliveryImportManagerState.RollingBack);
		}

		public void RollbackOutputs(DeliveryOutput[] outputs)
		{
			this.Batch<DeliveryOutput>(outputs,
				this.RollbackPassCount,
				OnBeginRollback,
				ex =>
				{
					OnEndRollback(ex);
					//no need to save the outpust since amit change it on the roleback
					//if (ex == null)
					//{
					//    foreach (DeliveryOutput output in outputs)
					//        output.Delivery.Save();

					//}
				},
				OnBeginRollbackPass,
				OnEndRollbackPass,
				OnRollbackOutput,
				DeliveryImportManagerState.RollingBack);
		}

		void Batch<T>(T[] items,
			int passes,
			Action onBegin,
			Action<Exception> onEnd,
			Action<int> onBeginPass,
			Action<int> onEndPass,
			Action<T, int> onItem,
			DeliveryImportManagerState activeState)
		{
			ThrowIfNotIdle();
			this.State = activeState;

			onBegin();
			Exception exception = null;

			try
			{
				for (int pass = 0; pass < passes; pass++)
				{
					onBeginPass(pass);
					for (int i = 0; i < items.Length; i++)
					{
						onItem(items[i], pass);
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

			try
			{
				onEnd(exception);
			}
			catch (Exception ex)
			{
				if (exception == null)
					exception = ex;
				else
					Log.Write("Failed to end delivery operation - probably because of another exception. See next log message.", ex);
			}
			finally
			{
				this.State = DeliveryImportManagerState.Idle;
			}

			
			// Throw exception if found
			if (exception != null)
				throw new Exception("Delivery operation failed while importing.", exception);
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
	
		protected virtual void OnBeginTransform() { }
		protected virtual void OnBeginTransformPass(int pass) { }
		protected abstract void OnTransform(Delivery delivery, int pass);
		protected virtual void OnEndTransformPass(int pass) { }
		protected virtual void OnEndTransform(Exception ex) { }
		protected virtual void OnDisposeTransform() { }

		protected virtual void OnBeginCommit() { }
		protected virtual void OnBeginCommitPass(int pass) { }
		protected abstract void OnCommit(Delivery delivery, int pass);
		protected virtual void OnEndCommitPass(int pass) { }
		protected virtual void OnEndCommit(Exception ex) { }
		protected virtual void OnDisposeCommit() { }

		protected virtual void OnBeginRollback() { }
		protected virtual void OnBeginRollbackPass(int pass) { }
		protected virtual void OnRollbackOutput(DeliveryOutput output, int pass) { }
		protected virtual void OnRollbackDelivery(Delivery delivery, int pass) { }
		protected virtual void OnEndRollbackPass(int pass) { }
		protected virtual void OnEndRollback(Exception ex) { }
		protected virtual void OnDisposeRollback() { }

		protected virtual void OnDispose() { }
	}

	public enum DeliveryImportManagerState
	{
		Idle,
		Importing,
		Transforming,
		Comitting,
		RollingBack
	}

}
