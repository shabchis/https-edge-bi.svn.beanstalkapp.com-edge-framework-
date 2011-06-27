using System;
using System.Collections.Generic;
using System.Linq;
using Edge.Data.Pipeline;
using Edge.Data.Objects;
using System.IO;
using System.Net;
using System.Text;
using Db4objects.Db4o.TA;

namespace Edge.Data.Pipeline
{
	/// <summary>
	/// Represents a raw data file that belongs to a delivery.
	/// </summary>
	public class DeliveryFile
	{
		Delivery _parentDelivery = null;
		Guid _fileID;
		DateTime _dateCreated = DateTime.Now;
		DateTime _dateModified = DateTime.Now;
		Dictionary<string, object> _parameters;
		DeliveryHistory<DeliveryOperation> _history;

		/// <summary>
		/// The delivery this file belongs to.
		/// </summary>
		public Delivery Delivery
		{
			get { return _parentDelivery; }
			internal set { _parentDelivery = value; }
		}

		/// <summary>
		/// Gets the unique ID of the file (-1 if unsaved).
		/// </summary>
		public Guid FileID
		{
			get { return _fileID; }
			internal set { _fileID = value; }
		}

		public FileFormat FileFormat { get; set; }

		/// <summary>
		/// Gets or sets the name of the delivery file.
		/// </summary>
		public string Name
		{
			get;
			set;
		}


		/// <summary>
		/// Gets or sets the URL from which the file is downloaded.
		/// </summary>
		public string SourceUrl
		{
			get;
			set;
		}

		/// <summary>
		/// Once it is downloaded, gets the location of the file in the FileManager-managed storage.
		/// </summary>
		public string Location
		{
			get;
			internal set;
		}


		/// <summary>
		/// Gets general parameters for use by services processing this delivery file.
		/// </summary>
		public Dictionary<string, object> Parameters
		{
			get { return _parameters ?? (_parameters = new Dictionary<string, object>()); }
			set { _parameters = value; }
		}

		/// <summary>
		/// Represents the history of operations on the delivery file. Each service that does an operation related to this file
		/// should add itself with the corresponding action.
		/// </summary>
		public DeliveryHistory<DeliveryOperation> History
		{
			get { return _history ?? (_history = new DeliveryHistory<DeliveryOperation>()); }
		}

		public Account Account
		{
			get;
			set;
		}

		/// <summary>
		/// Gets the date the delivery file was created.
		/// </summary>
		public DateTime DateCreated
		{
			get { return _dateCreated; }
		}

		/// <summary>
		/// Gets the date the delivery file was last modified.
		/// </summary>
		public DateTime DateModified
		{
			get { return _dateModified; }
		}

		public void Save()
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Gets the full path of the file after it was saved by FileManager to the internal file storage.
		/// </summary>
		//public FileInfo GetFileInfo()
		//{
		//	return FileManager.GetInfo(this.Parameters["FileRelativePath"].ToString());
		//}

		public Stream OpenContents(string subLocation = null, FileFormat fileFormat = FileFormat.Unspecified)
		{
			if (string.IsNullOrEmpty(this.Location))
				throw new InvalidOperationException("The delivery file does not have a valid file location. Make sure it has been downloaded properly.");
			if (this.Parameters.ContainsKey("InnerFileName") && !this.Location.Contains(this.Parameters["InnerFileName"].ToString()))
			{
				string fullLocation = string.Format("{0}{1}", this.Location, this.Parameters["InnerFileName"]);
				return FileManager.Open(subLocation == null ? fullLocation : Path.Combine(fullLocation, subLocation), fileFormat);
			}
			else
				return FileManager.Open(subLocation == null ? this.Location : Path.Combine(this.Location, subLocation), fileFormat);
		}

		void EnsureSaved()
		{
			if (this.FileID == Guid.Empty)
				throw new InvalidOperationException("Cannot download a delivery file before it has been saved.");
		}

		public DeliveryFileDownloadOperation Download(bool async = true)
		{
			EnsureSaved();
			string location = CreateLocation();
			return new DeliveryFileDownloadOperation(this, FileManager.Download(this.SourceUrl, location, async));
		}

		public DeliveryFileDownloadOperation Download(Stream sourceStream, bool async = true, long length = -1)
		{
			EnsureSaved();
			string location = CreateLocation();
			return new DeliveryFileDownloadOperation(this, FileManager.Download(sourceStream, location, async, length));
		}

		public DeliveryFileDownloadOperation Download(WebRequest request, bool async = true)
		{
			EnsureSaved();
			this.SourceUrl = request.RequestUri.ToString();
			string location = CreateLocation();
			return new DeliveryFileDownloadOperation(this, FileManager.Download(request, location, async));
		}

		private string CreateLocation()
		{
			StringBuilder location = new StringBuilder();
			location.AppendFormat(@"{0}\", this.Delivery.TargetLocationDirectory);
			if (this.Delivery.Account != null)
				location.AppendFormat(@"{0}\", this.Delivery.Account.ID);

			location.AppendFormat(@"{0}\{1}\{2}-{3}\{4}-{5}-{6}", _parentDelivery.DateCreated.ToString("yyyy-MM")/*0*/,
			_parentDelivery.DateCreated.ToString("dd")/*1*/,
				_parentDelivery.DateCreated.ToString("HHmm")/*2*/,
				this.Delivery.DeliveryID.ToString("N")/*3*/,
				DateTime.Now.ToString("yyyyMMdd@HHmm")/*4*/,
				this.FileID.ToString("N")/*5*/,
				this.Name == null ? string.Empty : this.Name);


			return location.ToString();
		}

		/// <summary>
		/// Gets info such as size and date created of the downloaded file. Equivelant to called FileManager.GetInfo(file.Location)
		/// </summary>
		/// <returns></returns>
		public FileInfo GetFileInfo()
		{
			if (String.IsNullOrWhiteSpace(this.Location))
				throw new InvalidOperationException("Cannot get info before the file has been downloaded.");

			return FileManager.GetInfo(this.Location);
		}
	}

	/// <summary>
	/// Contains information on an ongoing download operation of a delivery file.
	/// </summary>
	public class DeliveryFileDownloadOperation : FileDownloadOperation
	{
		FileDownloadOperation _innerOperation;

		internal DeliveryFileDownloadOperation(DeliveryFile file, FileDownloadOperation operation)
		{
			this.DeliveryFile = file;

			_innerOperation = operation;
			_innerOperation.Progressed += new EventHandler<ProgressEventArgs>(_innerOperation_Progressed);
			_innerOperation.Ended += new EventHandler<EndedEventArgs>(_innerOperation_Ended);
		}

		public DeliveryFile DeliveryFile
		{
			get;
			private set;
		}

		public override FileInfo FileInfo
		{
			get { return _innerOperation.FileInfo; }
		}

		public override System.IO.Stream Stream
		{
			get { return _innerOperation.Stream; }
		}

		public override bool IsAsync
		{
			get { return base.IsAsync; }
		}

		internal override string TargetPath
		{
			get { return _innerOperation.TargetPath; }
			set { _innerOperation.TargetPath = value; }
		}

		public override void Start()
		{
			_innerOperation.Start();
		}

		void _innerOperation_Progressed(object sender, ProgressEventArgs e)
		{
			RaiseProgress(e);
		}
		void _innerOperation_Ended(object sender, EndedEventArgs e)
		{
			this.DeliveryFile.Location = this.FileInfo.Location;
			RaiseEnded(e);
		}

	}

	public enum FileFormat
	{
		Unspecified = 1,
		GZip = 2
	}

}
