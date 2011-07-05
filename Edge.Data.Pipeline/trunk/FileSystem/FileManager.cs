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
		/// Does the actual downloading
		/// </summary>
		/// <param name="o">InternalDownloadParams containing relevant data.</param>
		internal static void InternalDownload(object o)
		{
			var operation = (FileDownloadOperation)o;

			var progressEventArgs = new ProgressEventArgs(0, operation.FileInfo.TotalBytes);
			FileStream outputStream = File.Create(operation.TargetPath);

			if (operation.Stream == null)
			{
				if (operation.Request == null)
					throw new InvalidOperationException("A download operation needs either a stream or web request object to start.");

				if (String.IsNullOrEmpty(operation.RequestBody))
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
						operation.RaiseEnded(new EndedEventArgs() { Success = false, Exception = ex });
						return;
					}
				}

				// Try to get the response stream from the web request
				WebResponse response;
				try { response = operation.Request.GetResponse(); }
				catch (Exception ex)
				{
					operation.Success = false;
					operation.RaiseEnded(new EndedEventArgs() { Success = false, Exception = ex });
					return;
				}

				operation.Stream = response.GetResponseStream();
				progressEventArgs.TotalBytes = operation.TotalBytes = response.ContentLength;
				operation.RaiseProgress(progressEventArgs);
			}

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
					operation.DownloadedBytes = progressEventArgs.DownloadedBytes = totalBytesRead;
					operation.RaiseProgress(progressEventArgs);
				}
				outputStream.Close();

				// Update the file info with physical file info
				System.IO.FileInfo f = new System.IO.FileInfo(operation.TargetPath);
				operation.FileInfo.TotalBytes = f.Length;
				operation.FileInfo.FileCreated = f.CreationTime;

				// Notify that we have succeeded
				operation.Success = true;
				operation.RaiseEnded(new EndedEventArgs() { Success = true });
			}
		}

		// Open operations
		// =========================================

		/// <summary>
		/// Opens a file from the specified location.
		/// </summary>
		/// <param name="location">Relative location of file in the FileManager system.</param>
		public static Stream Open(string location, FileFormat fileFormat=FileFormat.Unspecified)
		{
			return Open(GetInfo(location,fileFormat),fileFormat);
		}

		/// <summary>
		/// Opens the file represented by the FileInfo object.
		/// </summary>
		public static Stream Open(FileInfo fileInfo, FileFormat fileFormat = FileFormat.Unspecified)
		{
			Stream stream;
			if (string.IsNullOrEmpty(fileInfo.ZipLocation)) //not zip
			{
				string fullPath = Path.Combine(AppSettings.Get(typeof(FileManager), "RootPath"), fileInfo.Location);
				stream = File.OpenRead(fullPath);
			}
			else //Zip File
			{
				//TODO: TEMPORARLY JUST FOR TESTS TALK WITH DORON
				if (fileFormat==FileFormat.Unspecified)
				{
					FileStream zipStream = File.OpenRead(fileInfo.ZipLocation);
					ZipFile zipFile = new ZipFile(zipStream);
					ZipEntry zipEntry = zipFile.GetEntry(fileInfo.FileName);
					stream = zipFile.GetInputStream(zipEntry);
				}
				else
				{
					Stream fs = new FileStream(fileInfo.ZipLocation, FileMode.Open, FileAccess.Read);
					GZipInputStream gzipStream = new GZipInputStream(fs);
					stream = gzipStream;

					
					
				}
				
				
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
		public static FileInfo GetInfo(string location,FileFormat fileFormat=FileFormat.Unspecified)
		{
			Uri uri;


			uri = GetRelativeUri(location);


			// Get full path
			string fullPath = Path.Combine(AppSettings.Get(typeof(FileManager), "RootPath"), uri.ToString());

			bool isZip = false;
			string directory = string.Empty;
			string file = string.Empty;
			FileInfo fileInfo = new FileInfo() { Location = uri.ToString() };

			if (!File.Exists(fullPath))
			{
				isZip = true;
				directory = Path.GetDirectoryName(fullPath);
				while (Directory.Exists(directory))
				{
					directory = Path.GetDirectoryName(directory);
				}
				//if (!File.Exists(directory))
				//throw new FileNotFoundException();

				file = fullPath.Replace(directory + "\\", string.Empty);
			}

			if (isZip)
			{
				fileInfo.ZipLocation = directory;
				fileInfo.Location = uri.ToString();

				if (fileFormat==FileFormat.Unspecified)
				{
					using (ZipFile zipFile = new ZipFile(fileInfo.ZipLocation))
					{
						ZipEntry zipEntry = zipFile.GetEntry(file);
						fileInfo.TotalBytes = zipEntry.Size;
						fileInfo.FileCreated = zipEntry.DateTime; //TODO: CHECK THE MEANING OF ZIP DATETIME
					}
				}
				else
				{
					System.IO.FileInfo f = new System.IO.FileInfo(fileInfo.ZipLocation);
					fileInfo.TotalBytes = f.Length;
					fileInfo.FileCreated = f.CreationTime;
					
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



}
