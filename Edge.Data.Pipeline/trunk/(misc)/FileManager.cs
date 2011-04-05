using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System.Data.SqlClient;
using ICSharpCode.SharpZipLib.Zip;
using System.Collections;


namespace Edge.Data.Pipeline
{
    public static class FileManager 
    {
        public static string _rootPath;

		public static void Download(DeliveryFile file, Action<double> onProgress = null)
		{
			throw new NotImplementedException();
		}

		public static string Download(Uri url, Action<double> onProgress = null)
		{
			throw new NotImplementedException();
		}

		public static string Download(Stream stream, Action<double> onProgress = null)
		{
			throw new NotImplementedException();
		}

		public static FileInfo GetFileSystemInfo(DeliveryFile file)
		{
			if (String.IsNullOrWhiteSpace(file.SavedPath))
				throw new InvalidOperationException(String.Format("The delivery file{0} does not have a SavedPath; it might not have been retrieved yet.",
					file.Name == null ? null : " '" + file.Name + "'"
					));

			return GetFileSystemInfo(file.SavedPath);
		}

		public static FileInfo GetFileSystemInfo(string path)
		{
			return new FileInfo(path);
		}

		#region Ronen
		//===========================================================
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

		//===========================================================
		#endregion
	}
}
    