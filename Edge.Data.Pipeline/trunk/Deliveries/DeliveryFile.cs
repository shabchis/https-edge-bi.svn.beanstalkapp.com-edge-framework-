﻿using System;
using System.Collections.Generic;
using Edge.Data.Objects;
using System.IO;
using System.Net;
using System.Text;

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
		DeliveryHistory _history;

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

		public FileCompression FileFormat { get; set; }

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
		public DeliveryHistory History
		{
			get { return _history ?? (_history = new DeliveryHistory()); }
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

		// <summary>
		// Gets the full path of the file after it was saved by FileManager to the internal file storage.
		// </summary>
		//public FileInfo GetFileInfo()
		//{
		//	return FileManager.GetInfo(this.Parameters["FileRelativePath"].ToString());
		//}

		/// <summary>
		/// Opens the file contents as a readable stream.
		/// </summary>
		/// <param name="subLocation"></param>
		/// <param name="compression"></param>
		/// <returns></returns>
		public Stream OpenContents(string subLocation = null, FileCompression compression = FileCompression.None)
		{
			if (string.IsNullOrEmpty(this.Location))
				throw new InvalidOperationException("The delivery file does not have a valid file location. Make sure it has been downloaded properly.");

			return FileManager.Open(
				location:
					subLocation != null ? Path.Combine(this.Location, subLocation) : this.Location,
				compression:
					compression
			);
		}

		void EnsureSaved()
		{
			if (this.FileID == Guid.Empty)
				throw new InvalidOperationException("Cannot download a delivery file before it has been saved.");
		}

		public DeliveryFileDownloadOperation Download()
		{
			EnsureSaved();
			string location = CreateLocation();
			return new DeliveryFileDownloadOperation(this, this.SourceUrl, location);
		}

		public DeliveryFileDownloadOperation Download(Stream sourceStream, long length = -1)
		{
			EnsureSaved();
			string location = CreateLocation();
			return new DeliveryFileDownloadOperation(this, sourceStream, location, length);
		}

		public DeliveryFileDownloadOperation Download(WebRequest request)
		{
			EnsureSaved();
			this.SourceUrl = request.RequestUri.ToString();
			string location = CreateLocation();
			return new DeliveryFileDownloadOperation(this, request, location);
		}

		private string CreateLocation()
		{
			StringBuilder locationBuilder = new StringBuilder();
			string location = string.Empty;
			locationBuilder.AppendFormat(@"{0}\", this.Delivery.TargetLocationDirectory);
			if (this.Delivery.Account != null)
				locationBuilder.AppendFormat(@"{0}\", this.Delivery.Account.ID);

			locationBuilder.AppendFormat(@"{0}\{1}\{2}-{3}\{4}-{5}-{6}", _parentDelivery.DateCreated.ToString("yyyy-MM")/*0*/,
			_parentDelivery.DateCreated.ToString("dd")/*1*/,
				_parentDelivery.DateCreated.ToString("HHmm")/*2*/,
				this.Delivery.DeliveryID.ToString("N")/*3*/,
				this.Delivery.TargetPeriod.Start.ToDateTime().ToString("yyyyMMdd")/*4*/,
				this.FileID.ToString("N")/*5*/,
				this.Name == null ? string.Empty : this.Name);

			location = locationBuilder.ToString();
			if (location.Length > 260)
			{
				int diff = locationBuilder.ToString().Length - 260;
				string shortDeliveryId = Delivery.DeliveryID.ToString("N").Remove(locationBuilder.ToString().Length - diff);
				location= location.Replace(Delivery.DeliveryID.ToString("N"), shortDeliveryId);

			}


			return location;
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
		private void Init(DeliveryFile file, string targetLocation)
		{
			this.DeliveryFile = file;
			this.SetTargetLocation(targetLocation);
			this.Ended += new EventHandler(this.OnEnded); 
		}

		internal DeliveryFileDownloadOperation(DeliveryFile file, string sourceUrl, string targetLocation)
			: base(sourceUrl, targetLocation)
		{
			Init(file, targetLocation);
		}

		internal DeliveryFileDownloadOperation(DeliveryFile file, WebRequest request, string targetLocation)
			: base(request, targetLocation)
		{
			Init(file, targetLocation);
		}

		internal DeliveryFileDownloadOperation(DeliveryFile file, Stream sourceStream, string targetLocation, long length = -1)
			: base(sourceStream, targetLocation, length)
		{
			Init(file, targetLocation);
		}

		public DeliveryFile DeliveryFile
		{
			get;
			private set;
		}

		void OnEnded(object sender, EventArgs e)
		{
			this.DeliveryFile.Location = this.FileInfo.Location;
		}

	}

}
