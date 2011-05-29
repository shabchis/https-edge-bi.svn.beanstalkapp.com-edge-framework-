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
	public class DeliveryFile
	{
		Delivery _parentDelivery;
		Guid _fileID;
		DateTime _dateCreated = DateTime.Now;
		DateTime _dateModified = DateTime.Now;
		Dictionary<string, object> _parameters;
		DeliveryHistory<DeliveryOperation> _history;
		private string _location;

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

		public string Location
		{
			get
			{
				return _location;	// delivery.TargetLocationDirectory / {AccountID - if present} / yyyyMM / dd / DeliveryID / yyyyMMdd@hhmm-{df.FileID}{-df.Name}
			}
			internal set
			{
				_location = value;

			}
		}

		public DeliveryFileDownloadOperation Download(bool async = true)
		{
			string location = CreateLocation();
			return new DeliveryFileDownloadOperation(this, FileManager.Download(this.SourceUrl, location, async));
		}

		public DeliveryFileDownloadOperation Download(Stream sourceStream, bool async = true,long length=-1)
		{
			string location = CreateLocation();
			return new DeliveryFileDownloadOperation(this, FileManager.Download(sourceStream, location, async, length));
		}

		public DeliveryFileDownloadOperation Download(WebRequest request, bool async = true)
		{
			string location = CreateLocation();
			return new DeliveryFileDownloadOperation(this, FileManager.Download(request, location, async));
		}
		private string CreateLocation()
		{
			StringBuilder location = new StringBuilder();
			location.AppendFormat(@"{0}\", this.Delivery.TargetLocationDirectory);
			if (this.Delivery.Account != null)
				location.AppendFormat(@"{0}\", this.Delivery.Account.ID);

			location.AppendFormat(@"{0}\{1}\{2}-{3}\{4}-{5}\{6}", DateCreated.ToString("yyyyMM")/*0*/,
				DateCreated.ToString("dd")/*1*/,
				DateCreated.ToString("HHmm")/*2*/,
				this.Delivery.DeliveryID/*3*/,
				DateTime.Now.ToString("yyyyMMdd@HHmm")/*4*/,
				this.FileID/*5*/,
				this.Name == null ? string.Empty : this.Name);
			return location.ToString();
		}

		
	}

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

		public DeliveryFile DeliveryFile { get; private set; }

		void _innerOperation_Progressed(object sender, ProgressEventArgs e)
		{
			RaiseProgress(e);
		}
		void _innerOperation_Ended(object sender, EndedEventArgs e)
		{
			this.DeliveryFile.Location = this.FileInfo.Location;
			RaiseEnded(e);
		}

		public override FileInfo FileInfo
		{
			get { return _innerOperation.FileInfo; }
		}

		public override System.IO.Stream Stream
		{
			get { return base.Stream; }
		}
	}

}
