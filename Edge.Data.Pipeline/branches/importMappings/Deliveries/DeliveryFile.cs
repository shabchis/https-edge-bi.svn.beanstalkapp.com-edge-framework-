using System;
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
	public class DeliveryFile: IDeliveryChild
	{
		Dictionary<string, object> _parameters;

		public DeliveryFile()
		{
			this.DateCreated = DateTime.Now;
			this.DateModified = DateTime.Now;
		}

		/// <summary>
		/// The delivery this file belongs to.
		/// </summary>
		public Delivery Delivery { get; internal set; }

		/// <summary>
		/// Gets the unique ID of the file;
		/// </summary>
		public Guid FileID { get; internal set; }

		/// <summary>
		/// 
		/// </summary>
		public FileCompression FileCompression { get; set; }

		/// <summary>
		/// Gets or sets the name of the delivery file.
		/// </summary>
		public string Name { get; set; }

		/// <summary>
		/// Gets or sets the URL from which the file is downloaded.
		/// </summary>
		public string SourceUrl { get; set; }

		/// <summary>
		/// Once it is downloaded, gets the location of the file in the FileManager-managed storage.
		/// </summary>
		public string Location { get; internal set; }

		/// <summary>
		/// Gets the date the delivery file was created.
		/// </summary>
		public DateTime DateCreated { get; internal set; }

		/// <summary>
		/// Gets the date the delivery file was last modified.
		/// </summary>
		public DateTime DateModified { get; internal set; }

		/// <summary>
		/// 
		/// </summary>
		public DeliveryFileStatus Status { get; set; }

		/// <summary>
		/// Gets general parameters for use by services processing this delivery file.
		/// </summary>
		public Dictionary<string, object> Parameters
		{
			get { return _parameters ?? (_parameters = new Dictionary<string, object>()); }
			set { _parameters = value; }
		}
		/// <summary>
		/// Describe the name+size+modifiedday
		/// </summary>
		public string FileSignature { get; set; }
		/// <summary>
		/// Opens the file contents as a readable stream.
		/// </summary>
		public Stream OpenContents(string subLocation = null, FileCompression compression = FileCompression.None, ArchiveType archiveType = ArchiveType.None)
		{
			if (string.IsNullOrEmpty(this.Location))
				throw new InvalidOperationException("The delivery file does not have a valid file location. Make sure it has been downloaded properly.");

			return FileManager.Open(
				location:
					subLocation != null ? Path.Combine(this.Location, subLocation) : this.Location,
				compression:
					compression,
				archiveType:
					archiveType
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

		public DeliveryFileDownloadOperation Download(WebRequest request, long length = -1)
		{
			EnsureSaved();
			this.SourceUrl = request.RequestUri.ToString();
			string location = CreateLocation();
			return new DeliveryFileDownloadOperation(this, request, location, length);
		}

		public string CreateLocation()
		{
			StringBuilder locationBuilder = new StringBuilder();
			string location = string.Empty;
			locationBuilder.AppendFormat(@"{0}\", this.Delivery.FileDirectory);
			if (this.Delivery.Account != null)
				locationBuilder.AppendFormat(@"{0}\", this.Delivery.Account.ID);

			locationBuilder.AppendFormat(@"{0}\{1}\{2}-{3}\{4}-{5}-{6}", this.Delivery.DateCreated.ToString("yyyy-MM")/*0*/,
			this.Delivery.DateCreated.ToString("dd")/*1*/,
				this.Delivery.DateCreated.ToString("HHmm")/*2*/,
				this.Delivery.DeliveryID.ToString("N")/*3*/,
				this.Delivery.TimePeriodDefinition.Start.ToDateTime().ToString("yyyyMMdd")/*4*/,
				this.FileID.ToString("N")/*5*/,
				this.Name == null ? string.Empty : this.Name);

			location = locationBuilder.ToString();
			if (location.Length > 260)
			{
				int diff = locationBuilder.ToString().Length - 260;
				string shortDeliveryId = Delivery.DeliveryID.ToString("N").Remove(locationBuilder.ToString().Length - diff);
				location = location.Replace(Delivery.DeliveryID.ToString("N"), shortDeliveryId);

			}


			return location;
		}

		/// <summary>
		/// Gets info such as size and date created of the downloaded file. Equivelant to called FileManager.GetInfo(file.Location)
		/// </summary>
		/// <returns></returns>
		public FileInfo GetFileInfo(ArchiveType archiveType = ArchiveType.None)
		{
			if (String.IsNullOrWhiteSpace(this.Location))
				throw new InvalidOperationException("Cannot get info before the file has been downloaded.");

			return FileManager.GetInfo(this.Location, archiveType);
		}

		#region IDeliveryChild Members

		string IDeliveryChild.Key
		{
			get { return this.Name; }
		}

		Delivery IDeliveryChild.Delivery
		{
			get
			{
				return this.Delivery;
			}
			set
			{
				this.Delivery = value;
			}
		}

		#endregion
	}

	

}
