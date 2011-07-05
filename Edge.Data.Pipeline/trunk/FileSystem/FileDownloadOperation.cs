﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using Edge.Core.Configuration;
using System.Net;

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
		public string RequestBody { get; set; }

		internal string TargetPath { get; set; }

		public event EventHandler<ProgressEventArgs> Progressed;
		public event EventHandler<EndedEventArgs> Ended;

		private Thread _downloadThread;
		
		public double Progress
		{
			get { return DownloadedBytes / TotalBytes; }
		}
	
		#region Constructors
		// ---------------------------

		protected FileDownloadOperation()
		{
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


			HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(uri);
			request.Method = "GET";
			request.Timeout = (int)TimeSpan.FromDays(7).TotalMilliseconds;
			request.UserAgent = FileManager.UserAgentString;

			this.Request = request;
		}

		/// <summary>
		/// Downloads a file using the a web request.
		/// </summary>
		public FileDownloadOperation(WebRequest request, string targetLocation)
		{
			SetTargetLocation(targetLocation);

			if (request is HttpWebRequest)
			{
				// force user agent string, for good internet behavior :-)
				var httpRequest = (HttpWebRequest)request;
				if (String.IsNullOrEmpty(httpRequest.UserAgent))
					httpRequest.UserAgent = FileManager.UserAgentString;
			}

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
			this.TotalBytes = sourceStream.Length;
		}


		// ---------------------------
		#endregion

		#region Methods
		// ---------------------------

		internal protected void RaiseProgress(ProgressEventArgs e)
		{
			if (this.Progressed != null)
				this.Progressed(this, e);
		}
		internal protected void RaiseEnded(EndedEventArgs e)
		{
			if (this.Ended != null)
				this.Ended(this, e);
		}

		public virtual void Start()
		{
			this.DownloadedBytes = 0;

			_downloadThread = new Thread(FileManager.InternalDownload);
			_downloadThread.Start(this);
		}

		public virtual void Wait()
		{
			if (_downloadThread == null)
				throw new InvalidOperationException("The operation has not been started yet, so Wait cannot be called now.");

			_downloadThread.Join();
		}

		public virtual void EnsureSuccess()
		{
			if (_downloadThread == null)
				throw new InvalidOperationException("The operation has not been started yet.");

			if ((int)(_downloadThread.ThreadState & ThreadState.Running) > 0)
				throw new InvalidOperationException("The operation is still running.");

			if (!this.Success)
				throw this.Exception;
		}

		// ---------------------------
		#endregion
	}


	public class ProgressEventArgs : EventArgs
	{
		public long DownloadedBytes { get; internal set; }
		public long TotalBytes { get; internal set; }

		public ProgressEventArgs(long downloaded, long total)
		{
			this.DownloadedBytes = downloaded;
			this.TotalBytes = total;
		}

		public double Progress
		{
			get { return this.DownloadedBytes / this.TotalBytes; }
		}
	}
	public class EndedEventArgs : EventArgs
	{
		public bool Success;
		public Exception Exception;
	}
}
