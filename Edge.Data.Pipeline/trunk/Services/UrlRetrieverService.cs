using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using System.ServiceModel;
using System.Net;
using System.IO;
using Edge.Core.Services;
using System.Configuration;
using Edge.Data.Pipeline.Configuration;
using Edge.Core;
using Edge.Core.Utilities;
using Edge.Data.Pipeline;
using System.Threading;


namespace Edge.Data.Pipeline.Services
{
    public class UrlRetrieverService : PipelineService 
    {
		object _sync = new object();
		int _endedCount = 0;
		long _totalBytes = 0;
		double _progress = 0;
		List<DeliveryFile> _measured = new List<DeliveryFile>();
		EventHandler<ProgressEventArgs> _progressHandler;
		EventHandler<EndedEventArgs> _endedHandler;
		AutoResetEvent _waitForDownload;
		

		protected override ServiceOutcome DoPipelineWork()
		{
			_progressHandler = new EventHandler<ProgressEventArgs>(this.OperationProgressed);
			_endedHandler = new EventHandler<EndedEventArgs>(this.OperationEnded);
			_waitForDownload = new AutoResetEvent(false);

			foreach (DeliveryFile file in this.Delivery.Files)
			{
				// Ignore files that have already been downloaded (unless Overwrite is true)
				if (!Overwrite && file.History.Count(entry => entry.Operation == DeliveryOperation.Retrieved) > 0)
				{
					Log.Write(String.Format("Delivery file '{0}' (1) has already been retrieved.", file.Name, file.FileID), LogMessageType.Information);
					continue;
				}
				else
				{
					// Start downloading, and report progress of the download divided by total number of files
					DeliveryFileDownloadOperation operation = file.NewDownload();
					operation.Progressed += _progressHandler;
					operation.Ended += _endedHandler;
				}
			}
			
			_waitForDownload.WaitOne();

			return ServiceOutcome.Success;
		}

		void OperationProgressed(object sender, ProgressEventArgs e)
		{
			var operation = (DeliveryFileDownloadOperation)sender;
			lock (_measured)
			{
				// If this is the first time an operation is progressed, add its size to the total size
				if (!_measured.Contains(operation.DeliveryFile))
				{
					_measured.Add(operation.DeliveryFile);
					_totalBytes += operation.FileInfo.TotalBytes;
				}
			}

			// Report progress out of 90%
			double progress = (e.DownloadedBytes / _totalBytes) * 0.9;
			if (progress > _progress)
			{
				_progress = progress;
				ReportProgress(_progress);
			}
		}

		void OperationEnded(object sender, EndedEventArgs e)
		{
			var operation = (DeliveryFileDownloadOperation)sender;
			operation.DeliveryFile.History.Add(DeliveryOperation.Retrieved, this.Instance.InstanceID);
			operation.DeliveryFile.Save();

			lock (_sync)
			{
				_endedCount++;
				if (_endedCount == this.Delivery.Files.Count)
					_waitForDownload.Set();
			}
		}
		
		public bool Overwrite
		{
			get
			{
				string redownload = this.Instance.Configuration.Options["Overwrite"];
				if (redownload != null)
					return Boolean.Parse(redownload);
				else
					return false;
			}
		}

		//[ConfigurationOption("Account.Admin")]
		//public bool AccountAdmin { get; private set; }

    }
}
