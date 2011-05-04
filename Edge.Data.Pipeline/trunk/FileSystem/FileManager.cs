using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System.Data.SqlClient;
using ICSharpCode.SharpZipLib.Zip;
using System.Collections;
using Edge.Data.Pipeline;
using System.Threading;
using Edge.Core.Configuration;


namespace Edge.Data.Pipeline
{
	public static class FileManager
	{
		// Consts and statics
		// =========================================
		public static string UserAgentString = String.Format("Edge File Manager (version {0})", System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString());


		// Download operations
		// =========================================

		/// <summary>
		/// Downloads a file from a URL.
		/// </summary>
		public static FileDownloadOperation Download(string sourceUrl, string targetLocation, bool async = true)
		{
			Uri uri;
			try { uri = new Uri(sourceUrl); }
			catch (Exception ex) {  throw new ArgumentException("Invalid source URL. Check inner exception for details.", "sourceUrl", ex); }

			HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(uri);
			request.Method = "GET";
			request.Timeout = (int)TimeSpan.FromDays(7).TotalMilliseconds;
			request.UserAgent = FileManager.UserAgentString;
			return Download(request, targetLocation, true);
		}

		/// <summary>
		/// Downloads a file using the a web request.
		/// </summary>
		public static FileDownloadOperation Download(WebRequest request, string targetLocation, bool async = true)
		{
			if (request is HttpWebRequest)
			{
				// force user agent string, for good internet behavior :-)
				var httpRequest = (HttpWebRequest) request;
				if (String.IsNullOrEmpty(httpRequest.UserAgent))
					httpRequest.UserAgent = FileManager.UserAgentString;
			}

			WebResponse response = request.GetResponse();
			return Download(response.GetResponseStream(), targetLocation, async, response.ContentLength);
		}

		/// <summary>
		/// Downloads a file from a raw stream.
		/// </summary>
		public static FileDownloadOperation Download(Stream sourceStream, string targetLocation, bool async = true, long length = -1)
		{
			Uri uri;
			try
			{
				//check how to ensure that targetLocation is relative
				uri = new Uri(targetLocation, UriKind.Relative);
			}
			catch (Exception ex)
			{
				throw new ArgumentException("Invalid target location - path must be relative. See inner exception for details.", "targetLocation", ex);
			}
			
			// Get full path
			string fullPath = Path.Combine(AppSettings.Get(typeof(FileManager), "RootPath"), uri.ToString());

			// Get length from stream only if length was not specified
			if (length <= 0 && sourceStream.CanSeek)
				length = sourceStream.Length;

			// Object returned to caller for monitoring progress
			FileDownloadOperation fileDownLoadOperation = new FileDownloadOperation()
			{
				IsAsync = async,
				FileInfo = new FileInfo()
				{
					Location = uri.ToString(),
					TotalBytes = length
				},
				Stream = sourceStream,
				TargetPath = fullPath
			};

			return fileDownLoadOperation;
		}

		/// <summary>
		/// Does the actual downloading
		/// </summary>
		/// <param name="o">InternalDownloadParams containing relevant data.</param>
		internal static void InternalDownload(object o)
		{
			var operation = (FileDownloadOperation) o;
			var streamReader = new StreamReader(operation.Stream,Encoding.UTF8,true);
			
			var progressEventArgs = new ProgressEventArgs() { TotalBytes = operation.FileInfo.TotalBytes };
			long bytesRead = 0;
			long notifyProgressEvery = 128;

			using (StreamWriter streamWriter = new StreamWriter(operation.TargetPath,false))
			{
				try
				{
					string line;
					while (!streamReader.EndOfStream)
					{
						line = streamReader.ReadLine();
						bytesRead += line.Length;
						streamWriter.WriteLine(line);
						if (bytesRead >= notifyProgressEvery)
						{
							notifyProgressEvery += 128;
							progressEventArgs.DownloadedBytes = bytesRead;
							operation.RaiseProgress(progressEventArgs);
						}

					}
					//char[] buffer = null; not working! file has nulls
					//while (!streamReader.EndOfStream)
					//{
					//    int size = streamReader.Peek();
					//    if (size>=4)
					//        size=4;
						
					//    buffer = new char[size];
					//    int read = streamReader.Read(buffer, 0, size);
						
					//    streamWriter.Write(buffer);

					//    bytesRead += read;
					//    if (bytesRead % notifyProgressEvery == 0)
					//    {
					//        // Report progress
					//        progressEventArgs.DownloadedBytes = bytesRead;
					//        operation.RaiseProgress(progressEventArgs);
					//    }
					//}
				}
				catch (Exception ex)
				{
					operation.RaiseEnded(new EndedEventArgs() { Success = false, Exception = ex });
				}

				// Update the file info with physical file info
				System.IO.FileInfo f = new System.IO.FileInfo(operation.TargetPath);
				operation.FileInfo.TotalBytes = f.Length;
				operation.FileInfo.FileCreated = f.CreationTime;

				// Notify that we have succeeded
				operation.RaiseEnded(new EndedEventArgs() { Success = true });
			}
		}

		// Open operations
		// =========================================

		/// <summary>
		/// Opens a file from the specified location.
		/// </summary>
		/// <param name="location">Relative location of file in the FileManager system.</param>
		public static Stream Open(string location)
		{
			return Open(GetInfo(location));
		}

		/// <summary>
		/// Opens the file represented by the FileInfo object.
		/// </summary>
		public static Stream Open(FileInfo fileInfo)
		{
			Stream stream;
			if (string.IsNullOrEmpty(fileInfo.ZipLocation)) //not zip
			{
				string fullPath = Path.Combine(AppSettings.Get(typeof(FileManager), "RootPath"), fileInfo.Location);
				stream = File.OpenRead(fullPath);
			}
			else //Zip File
			{
				FileStream zipStream = File.OpenRead(fileInfo.ZipLocation);
				ZipFile zipFile = new ZipFile(zipStream);
				ZipEntry zipEntry = zipFile.GetEntry(fileInfo.FileName);
				stream = zipFile.GetInputStream(zipEntry);
			}

			return stream;
		}

		// Info operations
		// =========================================

		public static FileInfo GetInfo(string location)
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

			// Get full path
			string fullPath = Path.Combine(AppSettings.Get(typeof(FileManager), "RootPath"), uri.ToString());

			bool isZip = false;
			string directory=string.Empty;
			string file=string.Empty;
			FileInfo fileInfo = new FileInfo() { Location = uri.ToString() };

			if (!File.Exists(fullPath))
			{
				isZip = true;
				directory = Path.GetDirectoryName(fullPath);
				while (Directory.Exists(directory))
				{
					directory = Path.GetDirectoryName(directory);
				}
				if (!File.Exists(directory))
					throw new FileNotFoundException();

				file = fullPath.Replace(directory+"\\", string.Empty);
			}

			if (isZip)
			{
				fileInfo.ZipLocation = directory;
				fileInfo.Location = uri.ToString();
				using (ZipFile zipFile=new ZipFile(fileInfo.ZipLocation))
				{
					ZipEntry zipEntry = zipFile.GetEntry(file);
					fileInfo.TotalBytes = zipEntry.Size;
					fileInfo.FileCreated = zipEntry.DateTime; //TODO: CHECK THE MEANING OF ZIP DATETIME
				}
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

		#region Ronen
		//===========================================================
		/*
		private static string GetDeliveryFilePath(string targetDir, DateTime targetDate, int deliveryID, string fileName, int? accountID)
        {
            if (accountID != null)
                targetDir = String.Format(@"{0}\Accounts\{1}",targetDir, accountID );

            string path = String.Format(@"{0}\{1:yyyy}\{1:MM}\{1:dd}\{2}\{3} [{4:yyyymmdd@hhmmssfff}]{5}",
                targetDir,
                targetDate,
                deliveryID,
                Path.GetFileNameWithoutExtension(fileName),
                DateTime.Now,
                Path.GetExtension(fileName)
                );
            return path;
        }
        
        private static FileInfo SetDeliveryFilePath(string filepath)
        {
            
            try
            {
                FileInfo lFileInfo = new FileInfo(filepath);
                if (!lFileInfo.Directory.Exists)
                {
                    lFileInfo.Directory.Create();
                }
                return lFileInfo;
            }
            catch(Exception ex)
            {
                throw ex;
            }
        }

        private static void DownloadFile(string downloadUrl, string FilePath)
        {
            // Open a connection to the URL where the report is available.
            HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(downloadUrl);
            HttpWebResponse response = (HttpWebResponse)webRequest.GetResponse();
            Stream httpStream = response.GetResponseStream();

            // Open the report file.
            FileInfo lFileInfo = SetDeliveryFilePath(FilePath);
            FileStream fileStream = new FileStream(
                lFileInfo.FullName,
                FileMode.Create);
            BinaryWriter binaryWriter = new BinaryWriter(fileStream);
            BinaryReader binaryReader = new BinaryReader(httpStream);
            try
            {
                // Read the report and save it to the file.
                int bufferSize = 100000;
                while (true)
                {
                    // Read report data from API.
                    byte[] buffer = binaryReader.ReadBytes(bufferSize);

                    // Write report data to file.
                    binaryWriter.Write(buffer);

                    // If the end of the report is reached, break out of the 
                    // loop.
                    if (buffer.Length != bufferSize)
                    {
                        break;
                    }
                }
            }
            finally
            {
                // Clean up.
                binaryWriter.Close();
                binaryReader.Close();
                fileStream.Close();
                httpStream.Close();
            }
        }

        private static void ZipFiles(string inputFolderPath, string outputPathAndFile, string password)
        {
            ArrayList ar = GenerateFileList(inputFolderPath); // generate file list
            int TrimLength = (Directory.GetParent(inputFolderPath)).ToString().Length;
            // find number of chars to remove     // from orginal file path
            TrimLength += 1; //remove '\'
            FileStream ostream;
            byte[] obuffer;
            string outPath = inputFolderPath + @"\" + outputPathAndFile;
            ZipOutputStream oZipStream = new ZipOutputStream(File.Create(outPath)); // create zip stream
            if (password != null && password != String.Empty)
                oZipStream.Password = password;
            oZipStream.SetLevel(9); // maximum compression
            ZipEntry oZipEntry;
            foreach (string Fil in ar) // for each file, generate a zipentry
            {
                oZipEntry = new ZipEntry(Fil.Remove(0, TrimLength));
                oZipStream.PutNextEntry(oZipEntry);

                if (!Fil.EndsWith(@"/")) // if a file ends with '/' its a directory
                {
                    ostream = File.OpenRead(Fil);
                    obuffer = new byte[ostream.Length];
                    ostream.Read(obuffer, 0, obuffer.Length);
                    oZipStream.Write(obuffer, 0, obuffer.Length);
                }
            }
            oZipStream.Finish();
            oZipStream.Close();
        }


        private static ArrayList GenerateFileList(string Dir)
        {
            ArrayList fils = new ArrayList();
            bool Empty = true;
            foreach (string file in Directory.GetFiles(Dir)) // add each file in directory
            {
                fils.Add(file);
                Empty = false;
            }

            if (Empty)
            {
                if (Directory.GetDirectories(Dir).Length == 0)
                // if directory is completely empty, add it
                {
                    fils.Add(Dir + @"/");
                }
            }

            foreach (string dirs in Directory.GetDirectories(Dir)) // recursive
            {
                foreach (object obj in GenerateFileList(dirs))
                {
                    fils.Add(obj);
                }
            }
            return fils; // return file list
        }


        private static string UnZipFiles(string zipPathAndFile, string outputFolder, string password, bool deleteZipFile)
        {
            string fullPath = string.Empty;
            if(outputFolder == string.Empty || outputFolder  == null)
                outputFolder = Path.GetDirectoryName(zipPathAndFile);
                
            ZipInputStream s = new ZipInputStream(File.OpenRead(zipPathAndFile));
            if (password != null && password != String.Empty)
                s.Password = password;
            ZipEntry theEntry;
            string tmpEntry = String.Empty;
            while ((theEntry = s.GetNextEntry()) != null)
            {
                string directoryName = outputFolder;
                string fileName = Path.GetFileName(theEntry.Name);
                // create directory 
                if (!Directory.Exists(directoryName))
                {
                    Directory.CreateDirectory(directoryName);
                }
                if (fileName != String.Empty)
                {
                    fullPath = directoryName + "\\" + theEntry.Name;
                    fullPath = fullPath.Replace("\\ ", "\\");
                    string fullDirPath = Path.GetDirectoryName(fullPath);
                    if (!Directory.Exists(fullDirPath)) Directory.CreateDirectory(fullDirPath);
                    FileStream streamWriter = File.Create(fullPath);
                    int size = 2048;
                    byte[] data = new byte[2048];
                    while (true)
                    {
                        size = s.Read(data, 0, data.Length);
                        if (size > 0)
                        {
                            streamWriter.Write(data, 0, size);
                        }
                        else
                        {
                            break;
                        }
                    }
                    streamWriter.Close();
                }
            }
            s.Close();
            if (deleteZipFile)
                File.Delete(zipPathAndFile);
            return fullPath;
        }
		*/
		//===========================================================
		#endregion
	}

	public class FileInfo
	{
		public string FileName { get { return Path.GetFileName(this.Location); } }
		public string Location { get; internal set; }
		public string ZipLocation { get; internal set; }
		public string ContentType { get; internal set; }
		public long TotalBytes { get; internal set; }
		public DateTime FileCreated { get; internal set; }
		public DateTime FileModified { get; internal set; }
		
	}

	public class FileDownloadOperation
	{
		public virtual FileInfo FileInfo { get;internal set; } 
		public virtual Stream Stream { get; internal set; }
		public event EventHandler<ProgressEventArgs> Progressed;
		public event EventHandler<EndedEventArgs> Ended;
		public bool IsAsync { get; set; }
		internal string TargetPath { get; set; }

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

		public void Start()
		{
			if (IsAsync)
			{
				Thread saveFileThread = new Thread(FileManager.InternalDownload);
				saveFileThread.Start(this);
			}
			else
			{
				FileManager.InternalDownload(this);
			}

		}

	}

	public class ProgressEventArgs : EventArgs
	{
		public long DownloadedBytes;
		public long TotalBytes;
	}
	public class EndedEventArgs : EventArgs
	{
		public bool Success;
		public Exception Exception;
	}

}
