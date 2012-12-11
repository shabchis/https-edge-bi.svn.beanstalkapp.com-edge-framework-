using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using Edge.Core.Configuration;
using System.Net;
using Edge.Core.Utilities;

namespace Edge.Data.Pipeline
{
	/// <summary>
	/// Contains information on an ongoing download operation.
	/// </summary>
	public class FileDownloadOperation
	{
		public long TotalBytes { get; internal set; }
		public long DownloadedBytes { get; internal set; }
		public bool Success { get; internal set; }
		public Exception Exception { get; internal set; }

		public FileInfo FileInfo { get; internal set; }
		public Stream Stream { get; internal set; }
		public WebRequest Request { get; internal set; }
		public WebResponse Response { get; internal set; }

		public string RequestBody { get; set; }

		public bool ErrorAsString { get; set; }
		public string ErrorBody { get; internal set; }

		internal string TargetPath { get; set; }

		public event EventHandler Progressed;
		public event EventHandler Ended;

		private ManualResetEventSlim _waitHandle;

		public double Progress
		{
			get { return TotalBytes < 1 ? 0 : DownloadedBytes / TotalBytes; }
		}

		#region Constructors
		// ---------------------------

		protected FileDownloadOperation()
		{
			TotalBytes = -1;
			DownloadedBytes = 0;
			ErrorAsString = false;
		}

		protected void SetTargetLocation(string targetLocation)
		{
			if (String.IsNullOrEmpty(targetLocation))
				return;

			Uri uri;
			uri = FileManager.GetRelativeUri(targetLocation);

			// Get full path
			string fullPath = Path.Combine(AppSettings.Get(typeof(FileManager), "RootPath"), uri.ToString());
			if (!Directory.Exists(Path.GetDirectoryName(fullPath)))
				Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

			this.TargetPath = fullPath;
			this.FileInfo = new FileInfo()
			{
				Location = uri.ToString(),
				TotalBytes = -1
			};
		}

		/// <summary>
		/// Downloads a file from a URL.
		/// </summary>
		public FileDownloadOperation(string sourceUrl, string targetLocation)
		{
			SetTargetLocation(targetLocation);
			Uri uri;
			try { uri = new Uri(sourceUrl); }
			catch (Exception ex) { throw new ArgumentException("Invalid source URL. Check inner exception for details.", "sourceUrl", ex); }

			WebRequest request = HttpWebRequest.Create(uri);

			if (request is HttpWebRequest)
			{
				var httpRequest = (HttpWebRequest)request;
				httpRequest.Method = "GET";
				httpRequest.Timeout = (int)TimeSpan.FromDays(7).TotalMilliseconds;
				httpRequest.UserAgent = FileManager.UserAgentString;
			}

			this.Request = request;
		}

		/// <summary>
		/// Downloads a file using the a web request.
		/// </summary>
		public FileDownloadOperation(WebRequest request, string targetLocation, long length = -1)
		{
			SetTargetLocation(targetLocation);

			if (request is HttpWebRequest)
			{
				// force user agent string, for good internet behavior :-)
				var httpRequest = (HttpWebRequest)request;
				if (String.IsNullOrEmpty(httpRequest.UserAgent))
					httpRequest.UserAgent = FileManager.UserAgentString;
			}

            if (length > 0)
                this.TotalBytes = length;

			this.Request = request;
		}

		/// <summary>
		/// Downloads a file from a raw stream.
		/// </summary>
		public FileDownloadOperation(Stream sourceStream, string targetLocation, long length = -1)
		{
			SetTargetLocation(targetLocation);

			// Get length from stream only if length was not specified
			if (length <= 0 && sourceStream.CanSeek)
				length = sourceStream.Length;

			this.Stream = sourceStream;
            this.TotalBytes = length;
		}


		// ---------------------------
		#endregion

		#region Methods
		// ---------------------------


		public void Start()
		{
			_waitHandle = new ManualResetEventSlim();
			this.DownloadedBytes = 0;
			OnStart();
		}

		protected virtual void OnStart()
		{
			var internalDownload = new Action<object>(FileManager.InternalDownload);
			internalDownload.BeginInvoke(this, result =>
			{
				// finish the async operation and catch exceptions that were waiting for EndInvoke
				try { internalDownload.EndInvoke(result); }
				catch (Exception ex)
				{
					this.Success = false;
					this.Exception = ex;
				}
				this.RaiseEnded();
			},
			null);
		}

		public void Wait()
		{
			if (_waitHandle == null)
				throw new InvalidOperationException("The operation has not been started yet.");
			if (!_waitHandle.IsSet)
				_waitHandle.Wait();
		}

		public void EnsureSuccess()
		{
			if (_waitHandle == null)
				throw new InvalidOperationException("The operation has not been started yet.");

			if (!_waitHandle.IsSet)
				throw new InvalidOperationException("The operation is still running.");

			OnEnsureSuccess();
		}

		protected virtual void OnEnsureSuccess()
		{
			if (!this.Success)
				throw this.Exception;
		}


		internal protected void RaiseProgress()
		{
			if (this.Progressed != null)
				this.Progressed(this, EventArgs.Empty);
		}
		internal protected void RaiseEnded()
		{
			if (this.Ended != null)
				this.Ended(this, EventArgs.Empty);

			_waitHandle.Set();
			_waitHandle.Dispose();
		}

		// ---------------------------
		#endregion
	}

}
