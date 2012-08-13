using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System.Data.SqlClient;
using ICSharpCode.SharpZipLib.Zip;
using ICSharpCode.SharpZipLib.GZip;
using System.Collections;
using Edge.Data.Pipeline;
using System.Threading;
using Edge.Core.Configuration;
using Edge.Core.Utilities;


namespace Edge.Data.Pipeline
{
	public static class FileManager
	{
		// Consts and statics
		// =========================================
		public static string UserAgentString = String.Format("Edge File Manager (version {0})", System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString());
		public const int MaxPathLength = 259;
		public const int MinValidFileSize = 1;


		// Download operations
		// =========================================

		/// <summary>
		/// Does the actual downloading
		/// </summary>
		/// <param name="o">InternalDownloadParams containing relevant data.</param>
		internal static void InternalDownload(object o)
		{
			var operation = (FileDownloadOperation)o;

			if (operation.Stream == null)
			{
				if (operation.Request == null)
					throw new InvalidOperationException("A download operation needs either a stream or web request to start.");

				if (!String.IsNullOrEmpty(operation.RequestBody))
				{
					try
					{
						using (StreamWriter writer = new StreamWriter(operation.Request.GetRequestStream()))
						{
							writer.Write(operation.RequestBody);
						}
					}
					catch (Exception ex)
					{
						operation.Success = false;
						operation.Exception = ex;
						return;
					}
				}

				// Try to get the response stream from the web request
				WebResponse response;
				try { operation.Response = response = operation.Request.GetResponse(); }
				catch (Exception ex)
				{
					operation.Success = false;
					operation.Exception = ex;

					WebException webex;
					if (ex is WebException && (webex = (WebException)ex).Status == WebExceptionStatus.ProtocolError)
					{
						operation.Response = response = webex.Response;
					}
					else
						return;
				}

				operation.Stream = response.GetResponseStream();
				
				if (operation.Exception == null && response.ContentLength >= 0 && response.ContentLength < MinValidFileSize)
					throw new FileDownloadException(String.Format("Response from {0} returned content-length less than {1}.", operation.Request.RequestUri, MinValidFileSize)); 
				
				if(operation.TotalBytes == -1)
					operation.TotalBytes = response.ContentLength;

				operation.RaiseProgress();
			}

			using (FileStream outputStream = File.Create(operation.TargetPath))
			{
				using (operation.Stream)
				{
					int bufferSize = 2 << int.Parse(AppSettings.Get(typeof(FileManager), "BufferSize"));
					byte[] buffer = new byte[bufferSize];

					int bytesRead = 0;
					int totalBytesRead = 0;
					while ((bytesRead = operation.Stream.Read(buffer, 0, bufferSize)) != 0)
					{
						totalBytesRead += bytesRead;
						outputStream.Write(buffer, 0, bytesRead);
						operation.DownloadedBytes = totalBytesRead;
						operation.RaiseProgress();
					}
					outputStream.Close();

					// Update the file info with physical file info
					System.IO.FileInfo f = new System.IO.FileInfo(operation.TargetPath);
					operation.FileInfo.TotalBytes = f.Length;
					operation.FileInfo.FileCreated = f.CreationTime;

					if (f.Length < MinValidFileSize)
						throw new FileDownloadException(String.Format("Downloaded file ({0}) was 0 bytes.", operation.FileInfo.Location)); 

					// Notify that we have succeeded
					if (operation.Exception == null)
						operation.Success = true;
				}
			}
		}

		// Open operations
		// =========================================

		/// <summary>
		/// Opens a file from the specified location.
		/// </summary>
		/// <param name="location">Relative location of file in the FileManager system.</param>
		public static Stream Open(string location, ArchiveType archiveType = ArchiveType.None, FileCompression compression = FileCompression.None)
		{
			
			return Open(GetInfo(location,archiveType),compression);
		}

		/// <summary>
		/// Opens the file represented by the FileInfo object.
		/// </summary>
		public static Stream Open(FileInfo fileInfo, FileCompression compression = FileCompression.None)
		{
			Stream stream;

			// Archive opening
			if (fileInfo.ArchiveLocation != null)
			{
				if (fileInfo.ArchiveType == ArchiveType.Zip)
				{
					FileStream zipStream = File.OpenRead(fileInfo.ArchiveLocation);
					ZipFile zipFile = new ZipFile(zipStream);
					ZipEntry zipEntry = zipFile.GetEntry(fileInfo.FileName);
					stream = zipFile.GetInputStream(zipEntry);
				}
				else
					throw new NotImplementedException("Only zip archives supported at this time.");
			}
			else
				stream = File.OpenRead(fileInfo.FullPath);

			// Decompression
			if (compression != FileCompression.None)
			{
				if (compression == FileCompression.Gzip)
					stream = new GZipInputStream(stream);
				else
					throw new NotImplementedException("Only gzip compression supported at this time.");
			}
			else
			{
				// Nothing to do here, since we already opened the file stream
			}

			return stream;
		}

		// Info operations
		// =========================================
		public static Uri GetRelativeUri(string location)
		{
			Uri uri;
			try
			{
				// ensure that targetLocation is relative
				uri = new Uri(location, UriKind.Relative);
			}
			catch (Exception ex)
			{
				throw new ArgumentException("Invalid file location - path must be relative. See inner exception for details.", "targetLocation", ex);
			}
			return uri;

		}
		public static FileInfo GetInfo(string location, ArchiveType archiveType = ArchiveType.None)
		{
			Uri uri = GetRelativeUri(location);

			// Get full path
			string fullPath = Path.Combine(AppSettings.Get(typeof(FileManager), "RootPath"), uri.ToString());

			string container = fullPath;
			string innerFile = string.Empty;
			FileInfo fileInfo = new FileInfo() { Location = uri.ToString(), FullPath = fullPath };

			if (fullPath.Length > FileManager.MaxPathLength || !File.Exists(fullPath) )
			{
				if (archiveType == ArchiveType.None)
					throw new ArgumentException("Specified file path does not exist. If it is an archive, set archiveType parameter.", "location");

				container = Path.GetDirectoryName(fullPath);
				while (container.Length > 0 && (container.Length > FileManager.MaxPathLength || !File.Exists(container)))
				{
					container = Path.GetDirectoryName(container);
				}

				innerFile = fullPath.Replace(container + Path.DirectorySeparatorChar, string.Empty);
			}

			if (archiveType != ArchiveType.None)
			{
				fileInfo.ArchiveLocation = container;
				fileInfo.ArchiveType = archiveType;
			}

			if (!String.IsNullOrEmpty(innerFile))
			{
				if (archiveType == ArchiveType.Zip)
				{
					using (ZipFile zipFile = new ZipFile(fileInfo.ArchiveLocation))
					{
						ZipEntry zipEntry = zipFile.GetEntry(innerFile);
						fileInfo.TotalBytes = zipEntry.Size;
						fileInfo.FileCreated = zipEntry.DateTime;
					}
				}
				else
					throw new NotImplementedException("Only zip archives supported at this time.");
			}
			else
			{
				System.IO.FileInfo ioFileInfo = new System.IO.FileInfo(fullPath);
				fileInfo.FileCreated = ioFileInfo.CreationTime;
				fileInfo.FileModified = ioFileInfo.LastWriteTime;
				fileInfo.TotalBytes = ioFileInfo.Length;
			}

			return fileInfo;
		}

		internal static string[] GetSubFiles(FileInfo fileInfo)
		{
			List<string> subFiles = new List<string>();
			if (fileInfo.ArchiveType == ArchiveType.None)
				throw new InvalidOperationException("Cannot get sub files for a non-archive path.");

			if (fileInfo.ArchiveType == ArchiveType.Zip)
			{
				using (ZipFile zipFile = new ZipFile(fileInfo.ArchiveLocation))
				{
					foreach (ZipEntry zipEntry in zipFile)
					{
						subFiles.Add(zipEntry.Name);

						
					}
				
				}
			}
			else
			{
				throw new NotSupportedException();
			}
			return subFiles.ToArray();
		}
	}

	public class FileInfo
	{
		internal string FullPath;
		public string FileName { get { return Path.GetFileName(this.Location); } }
		public string Location { get; internal set; }
		public string ArchiveLocation { get; internal set; }
		public string ContentType { get; internal set; }
		public long TotalBytes { get; internal set; }
		public DateTime FileCreated { get; internal set; }
		public DateTime FileModified { get; internal set; }
		public ArchiveType ArchiveType { get; internal set; }

		public string[] GetSubFiles()
		{
			return FileManager.GetSubFiles(this);
		}

		//public FileInfo GetSubFileInfo(string subFileName, ArchiveType subFileArchiveType = Pipeline.ArchiveType.None)
		//{
		//    return FileManager.GetInfo(Path.Combine(this.Location, subFileName), subFileArchiveType);
		//}

	}



	public enum FileCompression
	{
		None,
		Gzip
	}

	public enum ArchiveType
	{
		None,
		Zip,
		TarUncompressed,
		TarGzip
	}

	[Serializable]
	public class FileDownloadException : Exception
	{
		public FileDownloadException() { }
		public FileDownloadException(string message) : base(message) { }
		public FileDownloadException(string message, Exception inner) : base(message, inner) { }
		protected FileDownloadException(
		  System.Runtime.Serialization.SerializationInfo info,
		  System.Runtime.Serialization.StreamingContext context)
			: base(info, context) { }
	}

}
