using System;
using Edge.Core.Utilities;
using Edge.Data.Pipeline.Objects;

namespace Edge.Data.Pipeline
{
	public abstract class DeliveryManager: IDisposable
	{
		protected DeliveryManager(Guid serviceInstanceID)
		{
			State = DeliveryManagerState.Idle;
		}

		public DeliveryManagerState State
		{
			get;
			protected set;
		}

		public Delivery CurrentDelivery
		{
		    get;
		    protected set;
		}

		protected virtual int TransformPassCount
		{
			get { return 1; }
		}
		protected virtual int StagePassCount
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

		public void BeginImport(Delivery delivery, MetricsUnit sampleMetrics)
		{
			ThrowIfNotIdle();
			State = DeliveryManagerState.Importing;
			CurrentDelivery = delivery;
			
			OnBeginImport(sampleMetrics);
		}


		public void EndImport()
		{
			if (State != DeliveryManagerState.Importing)
				throw new InvalidOperationException("EndImport can only be called after BeginImport.");
			OnEndImport();

			CurrentDelivery.Save();
			CurrentDelivery = null;

			State = DeliveryManagerState.Idle;

			OnDisposeImport();
			OnDispose();
		}

		public void Transform(Delivery[] deliveries)
		{
			Batch(deliveries,
				TransformPassCount,
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
				DeliveryManagerState.Transforming);
		}

		public void Stage(Delivery[] deliveries)
		{
			Batch(deliveries,
				StagePassCount,
				OnBeginStage,
				ex =>
				{
					OnEndStage(ex);

					if (ex == null)
					{
						foreach (Delivery d in deliveries)
							d.Save();
					}
				},
				OnBeginStagePass,
				OnEndStagePass,
				OnStage,
				DeliveryManagerState.Staging);
		}

		public void Commit(Delivery[] deliveries)
		{
			Batch(deliveries,
				CommitPassCount,
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
				DeliveryManagerState.Comitting);
		}

		public void RollbackDeliveries(Delivery[] deliveries)
		{
			Batch(deliveries,
				RollbackPassCount,
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
				DeliveryManagerState.RollingBack);
		}

		public void RollbackOutputs(DeliveryOutput[] outputs)
		{
			Batch(outputs,
				RollbackPassCount,
				OnBeginRollback,
				OnEndRollback,
				OnBeginRollbackPass,
				OnEndRollbackPass,
				OnRollbackOutput,
				DeliveryManagerState.RollingBack);
		}

		void Batch<T>(T[] items,
			int passes,
			Action onBegin,
			Action<Exception> onEnd,
			Action<int> onBeginPass,
			Action<int> onEndPass,
			Action<T, int> onItem,
			DeliveryManagerState activeState)
		{
			ThrowIfNotIdle();
			State = activeState;

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
			catch (DeliveryConflictException)
			{
				throw;
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
					Log.Write(ToString(), "Failed to end delivery operation - probably because of another exception. See next log message.", ex);
			}
			finally
			{
				State = DeliveryManagerState.Idle;
			}

			
			// Throw exception if found
			if (exception != null)
				throw new Exception("Delivery operation failed while importing.", exception);
		}

		void ThrowIfNotIdle()
		{
			if (State != DeliveryManagerState.Idle)
				throw new InvalidOperationException("DeliveryImportManager is currently in a busy state.");
		}

		public void Dispose()
		{
			OnDisposeImport();
			OnDisposeStage();
			OnDisposeCommit();
			OnDisposeRollback();
			OnDispose();
		}

		protected virtual void OnBeginImport(MetricsUnit sampleMetrics) {}
		protected virtual void OnEndImport() { }
		protected virtual void OnDisposeImport() { }
	
		protected virtual void OnBeginTransform() { }
		protected virtual void OnBeginTransformPass(int pass) { }
		protected abstract void OnTransform(Delivery delivery, int pass);
		protected virtual void OnEndTransformPass(int pass) { }
		protected virtual void OnEndTransform(Exception ex) { }
		protected virtual void OnDisposeTransform() { }

		protected virtual void OnBeginStage() { }
		protected virtual void OnBeginStagePass(int pass) { }
		protected abstract void OnStage(Delivery delivery, int pass);
		protected virtual void OnEndStagePass(int pass) { }
		protected virtual void OnEndStage(Exception ex) { }
		protected virtual void OnDisposeStage() { }

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

	public enum DeliveryManagerState
	{
		Idle,
		Importing,
		Transforming,
		Staging,
		Comitting,
		RollingBack
	}
}
