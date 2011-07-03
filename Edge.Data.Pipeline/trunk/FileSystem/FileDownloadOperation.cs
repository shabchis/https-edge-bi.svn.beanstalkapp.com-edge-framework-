using System;
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

		public virtual FileInfo FileInfo { get; internal set; }
		public virtual Stream Stream { get; internal set; }
		public virtual WebRequest Request { get; internal set; }

		internal virtual string TargetPath { get; set; }

		public event EventHandler<ProgressEventArgs> Progressed;
		public event EventHandler<EndedEventArgs> Ended;

		private Thread _downloadThread;
		
		public double Progress
		{
			get { return DownloadedBytes / TotalBytes; }
		}
	
		#region Constructors
		// ---------------------------

		protected FileDownloadOperation(string targetLocation)
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
			: this(targetLocation)
		{
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
			: this(targetLocation)
		{
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
			: this(targetLocation)
		{
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
			_downloadThread.Join();
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
