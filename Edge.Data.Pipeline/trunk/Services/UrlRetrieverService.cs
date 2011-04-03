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


namespace Edge.Data.Pipeline
{
    public class DeliveryDownloaderService : PipelineService 
    {
		protected override ServiceOutcome DoWork()
		{
			// Download and show progress out of 75% (37.5% per file)
			foreach (DeliveryFile file in this.Delivery.Files)
			{
				// Ignore files that have already been downloaded (unless RedownloadAll is true)
				if (!RedownloadAll && file.History.Count(entry => entry.Operation == DeliveryOperation.Retrieved) > 0)
				{
					Log.Write(String.Format("Delivery file '{0}' (1) has already been retrieved.", file.Name, file.FileID), LogMessageType.Information);
					continue;
				}
				else
				{
					// Download the file, and report progress of the download divided by total number of files
					FileManager.Download(file, p => ReportProgress(p / this.Delivery.Files.Count));
					file.History.Add(DeliveryOperation.Retrieved, this.Instance.InstanceID);
					file.Save();
				}
			}

			return ServiceOutcome.Success;
		}

		public bool RedownloadAll
		{
			get
			{
				string redownload = this.Instance.Configuration.Options["RedownloadAll"];
				if (redownload != null)
					return Boolean.Parse(redownload);
				else
					return false;
			}
		}

    }
}
