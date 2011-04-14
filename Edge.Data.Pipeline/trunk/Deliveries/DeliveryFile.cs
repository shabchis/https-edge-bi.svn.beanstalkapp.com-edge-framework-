using System;
using System.Collections.Generic;
using System.Linq;
using Edge.Data.Pipeline.Readers;

namespace Edge.Data.Pipeline.Deliveries
{
	public class DeliveryFile
	{
		DateTime _dateCreated = DateTime.Now;
		DateTime _dateModified = DateTime.Now;
		Dictionary<string, object> _parameters;
		DeliveryHistory<DeliveryOperation> _history;

		/// <summary>
		/// Gets the unique ID of the file (-1 if unsaved).
		/// </summary>
		public int FileID
		{
			get { throw new NotImplementedException(); }
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
		/// Gets the full path of the file after it was saved by FileManager to the internal file storage.
		/// </summary>
		public FileInfo FileInfo
		{
			get;
			internal set;
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
		/// Gets or sets the type of reader that can read the contents of the file.
		/// </summary>
		public Type ReaderType
		{
			get;
			set;
		}

		/// <summary>
		/// Optional arguments that are passed to the reader when CreateReader is called.
		/// </summary>
		public object[] ReaderArguments
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

		/// <summary>
		/// Creates a new instance of this.ReaderType.
		/// </summary>
		/// <param name="args">Optional parameters for the reader constructor. If empty, this.ReaderArguments is used.</param>
		public IReader CreateReader(params object[] args)
		{
			return (IReader)Activator.CreateInstance(ReaderType, args);
		}

		/// <summary>
		/// Creates a new instance of this.ReaderType.
		/// </summary>
		/// <param name="args">Optional parameters for the reader constructor. If empty, this.ReaderArguments is used.</param>
		/// <typeparam name="T">The type of object read by this reader.</typeparam>
		public IReader<T> CreateReader<T>(params object[] args)
		{
			return (IReader<T>)CreateReader(args);
		}

		public void Save()
		{
			throw new NotImplementedException();
		}

		public DeliveryFileDownloadOperation Download(bool async = true)
		{
			// TODO: create 'targetLocation' using: DeliveryID, FileName, AccountID, TargetPeriod etc.
			return new DeliveryFileDownloadOperation(this, FileManager.Download(this.SourceUrl, "blah.xml", async));
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
