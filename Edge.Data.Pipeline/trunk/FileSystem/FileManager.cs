using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System.Data.SqlClient;
using ICSharpCode.SharpZipLib.Zip;
using System.Collections;
using Edge.Data.Pipeline.Deliveries;
using System.Threading;
using Edge.Core.Configuration;


namespace Edge.Data.Pipeline
{
	public static class FileManager
	{
		public static string _rootPath = AppSettings.GetAbsolute("RootPath");

		/// <summary>
		/// Download("http://sadasdsad", "Google/Adowrds/Accounts/7/asdasd.xml"
		/// </summary>
		/// <param name="url"></param>
		/// <param name="targetLocation">Path to save the file to, relative to the root file system folder.</param>
		/// <param name="async"></param>
		/// <returns></returns>
		public static FileDownloadOperation Download(string sourceUrl, string targetLocation, bool async = true)
		{
			Uri uri;
			try
			{
				uri = new Uri(sourceUrl);
			}
			catch (Exception ex)
			{

				throw new Exception("sourceUrl format is invalid: ", ex);
			}
			HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(uri);
			//TODO: CHECK IF METHOD IS ALWAYS GET
			request.Method = "GET";
			request.Timeout = 9999999;
			HttpWebResponse response = (HttpWebResponse)request.GetResponse();
			return Download(response.GetResponseStream(), targetLocation, true);





		}

		public static FileDownloadOperation Download(Stream sourceStream, string targetLocation, bool async = true)
		{
			Uri uri;
			try
			{
				//check how to ensure that targetLocation is relative
				uri = new Uri(targetLocation, UriKind.Relative);

			}
			catch (Exception ex)
			{

				throw new Exception("target Loacation format is invalid", ex);
			}
			//// Get full path
			string fullPath = Path.Combine(_rootPath, uri.ToString());

			FileDownloadOperation fileDownLoadOperation = new FileDownloadOperation();
			ProgressEventArgs progressEventArgs = new ProgressEventArgs();

			////save the file
			long length = 0;
			long postion = 0;
			if (sourceStream.CanSeek)
				length = sourceStream.Length;

			int counter = 0;
			Thread saveFileThread = new Thread(new ThreadStart(delegate()
				{
					StreamReader streamReader = new StreamReader(sourceStream);

					using (StreamWriter streamWriter = new StreamWriter(fullPath, false, Encoding.UTF8))
					{

						string str;
						try
						{
							while (!streamReader.EndOfStream)
							{
								str = streamReader.ReadLine();
								postion += str.Length;
								streamWriter.WriteLine(str);
								//TODO: REPORT PROGRESS IF CAN SEEK;
								counter++;

								if (counter == 10 && sourceStream.CanSeek)
								{
									counter = 0;
									//reportprogress;
									progressEventArgs.DownloadedBytes = postion;
									progressEventArgs.TotalBytes = length;
									fileDownLoadOperation.RaiseProgress(progressEventArgs);
								}
							}
						}
						catch (Exception ex)
						{

							fileDownLoadOperation.RaiseEnded(new EndedEventArgs() { Success = false, Exception = ex });
						}
						fileDownLoadOperation.RaiseEnded(new EndedEventArgs() { Success = true });
						//TODO OTHER WAY THE USE SYSTEM.IO.FILEINFO
						System.IO.FileInfo f = new System.IO.FileInfo(fullPath);
						FileInfo fileInfo = new FileInfo();
						fileInfo.Location = new Uri(fullPath);
						fileInfo.TotalBytes = f.Length;
						fileInfo.FileCreated = f.CreationTime;
						fileInfo.Extenstion = f.Extension;
						//TODO: OTHER PROPERTIES OF FILE INFO

						//TODO: DB4O SAVE FILEINFOOBJECT;

					}
				}));


			return fileDownLoadOperation;
		}
		// file.zip/hg.txt
		public static FileOpenOperation Open(string location, bool unzip = true)
		{
			FileOpenOperation fileOpenOperation;

			FileInfo fileInfo = GetInfo(location);
			FileStream fileStream=File.OpenRead(fileInfo.Location.ToString());
			Stream stream;
			if (fileInfo.Extenstion.ToLower().Trim() == "zip")
			{
				ZipFile zipFile = new ZipFile(fileStream);
				ZipEntry zipEntry = zipFile[0];
				stream = zipFile.GetInputStream(zipEntry);
				fileOpenOperation = new FileOpenOperation(fileInfo, stream);
			}
			else
				fileOpenOperation = new FileOpenOperation(fileInfo, fileStream);

			return fileOpenOperation;
		}

		public static FileInfo GetInfo(string location)
		{
			FileInfo fileInfo = null;
			Uri uri;
			try
			{
				uri = new Uri(location);
			}
			catch (Exception ex)
			{

				throw new Exception("location format is invalid: ", ex);
			}
			//TODO: GET FILE INFO FROM DB4O
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
		public Uri Location { get; internal set; }
		public string ContentType { get; internal set; }
		public long TotalBytes { get; internal set; }
		public DateTime FileCreated { get; internal set; }
		public DateTime FileModified { get; internal set; }
		public string Extenstion { get; internal set; } //Add by alon
	}

	public class FileDownloadOperation
	{
		public virtual FileInfo FileInfo { get; private set; }
		public virtual Stream Stream { get; internal set; }
		public event EventHandler<ProgressEventArgs> Progressed;
		public event EventHandler<EndedEventArgs> Ended;

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

	public class FileOpenOperation : IDisposable
	{
		public virtual FileInfo FileInfo { get; private set; }
		public virtual Stream Stream { get; private set; }

		internal FileOpenOperation(FileInfo info, Stream stream)
		{
			throw new NotImplementedException();
		}

		public virtual void Dispose()
		{

			Stream.Close();

			// TODO: if zip, clean up temp files
		}
	}

}
