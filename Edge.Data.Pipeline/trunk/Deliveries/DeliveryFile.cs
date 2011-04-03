using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Pipeline
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
		public string SavedPath
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
		public IRowReader CreateReader(params object[] args)
		{
			return (IRowReader)Activator.CreateInstance(ReaderType, args);
		}

		/// <summary>
		/// Creates a new instance of this.ReaderType.
		/// </summary>
		/// <param name="args">Optional parameters for the reader constructor. If empty, this.ReaderArguments is used.</param>
		/// <typeparam name="RowT">The type of row read by this reader.</typeparam>
		public IRowReader<RowT> CreateReader<RowT>(params object[] args)
		{
			return (IRowReader<RowT>)CreateReader(args);
		}

		public void Save()
		{
			throw new NotImplementedException();
		}

	}

}
