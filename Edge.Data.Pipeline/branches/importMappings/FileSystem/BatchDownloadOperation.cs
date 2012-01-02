using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.IO;
using Edge.Core.Utilities;

namespace Edge.Data.Pipeline
{

	/// <summary>
	/// 
	/// </summary>
	public class BatchDownloadOperation : FileDownloadOperation, IList<FileDownloadOperation>
	{
		public bool StopOnError { get; set; }
		public int MaxConcurrent { get; set; }

		private List<FileDownloadOperation> _operations = new List<FileDownloadOperation>();
		private int _next = 0;
		private int _current = 0;
		private object _queueLock = new object();
		private bool _started = false;

		public BatchDownloadOperation()
		{
			this.MaxConcurrent = 5;
		}

		protected override void OnStart()
		{

			// Hook up all events before starting
			foreach (FileDownloadOperation operation in _operations)
			{
				operation.Progressed += new EventHandler(operation_Progressed);
				operation.Ended += new EventHandler(operation_Ended);
				this.TotalBytes += operation.TotalBytes;
			}

			_next = 0;
			_current = 0;

			while (_current < this.MaxConcurrent && _next < _operations.Count)
			{
				FileDownloadOperation nextOperation = _operations[_next];

				_next++;
				_current++;

				nextOperation.Start();
			}
		}

		protected override void OnEnsureSuccess()
		{
			BatchDownloadException ex = null;

			foreach (FileDownloadOperation op in _operations)
			{
				if (op.Success)
					continue;

				// operation failed
				if (ex == null)
					ex = new BatchDownloadException();

				ex.InnerExceptions.Add(op.Exception);
			}

			if (ex != null)
				throw ex;
		}

		void operation_Progressed(object sender, EventArgs e)
		{
			long downloaded = 0;
			long total = 0;
			_operations.All(operation =>
			{
				downloaded += operation.DownloadedBytes;
				total += operation.TotalBytes;
				return true;
			});

			this.DownloadedBytes = downloaded;
			this.TotalBytes = total;

			this.RaiseProgress();
		}

		void operation_Ended(object sender, EventArgs e)
		{
			lock (_queueLock)
			{
				_current--;

				if (_next >= _operations.Count && _current == 0)
				{
					// We're done, since there's no next to progress to and all the current are finished
					this.Success = _operations.TrueForAll(op => op.Success);
					RaiseEnded();
				}
				else if (_next < _operations.Count)
				{
					// There's a next download to start
					FileDownloadOperation nextOperation = _operations[_next];
					_current++;
					_next++;
					nextOperation.Start();
				}
			}
		}

		private void ThrowIfStarted()
		{
			if (this._started)
				throw new InvalidOperationException("Batch download operation cannot be changed once it is started.");
		}

		#region List read
		// ---------------------------

		public int IndexOf(FileDownloadOperation item)
		{
			return _operations.IndexOf(item);
		}

		public bool Contains(FileDownloadOperation item)
		{
			return _operations.Contains(item);
		}

		public void CopyTo(FileDownloadOperation[] array, int arrayIndex)
		{
			_operations.CopyTo(array, arrayIndex);
		}

		public int Count
		{
			get { return _operations.Count; }
		}

		public bool IsReadOnly
		{
			get { return _started; }
		}

		IEnumerator<FileDownloadOperation> IEnumerable<FileDownloadOperation>.GetEnumerator()
		{
			return _operations.GetEnumerator();
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return ((System.Collections.IEnumerable)_operations).GetEnumerator();
		}

		// ---------------------------
		#endregion

		#region List write
		// ---------------------------

		public void Insert(int index, FileDownloadOperation item)
		{
			ThrowIfStarted();
			_operations.Insert(index, item);
		}

		public void RemoveAt(int index)
		{
			ThrowIfStarted();
			_operations.RemoveAt(index);
		}

		public FileDownloadOperation this[int index]
		{
			get
			{
				return _operations[index];
			}
			set
			{
				ThrowIfStarted();
				_operations[index] = value;
			}
		}


		public void Add(FileDownloadOperation item)
		{
			ThrowIfStarted();
			_operations.Add(item);
		}

		public void Clear()
		{
			ThrowIfStarted();
			_operations.Clear();
		}


		public bool Remove(FileDownloadOperation item)
		{
			ThrowIfStarted();
			return _operations.Remove(item);
		}

		// ---------------------------
		#endregion

	}

	[Serializable]
	public class BatchDownloadException : Exception
	{
		public BatchDownloadException() { }
		public BatchDownloadException(string message) : base(message) { }
		protected BatchDownloadException(
		  System.Runtime.Serialization.SerializationInfo info,
		  System.Runtime.Serialization.StreamingContext context)
			: base(info, context) { }

		public readonly List<Exception> InnerExceptions = new List<Exception>();

		public override string ToString()
		{
			StringBuilder builder = new StringBuilder();
			foreach (Exception ex in this.InnerExceptions)
			{
				builder.Append(ex.ToString());
				builder.Append("\n\n======================================\n\n");
			}

			builder.Append(base.ToString());
			return builder.ToString();
		}
	}
}
