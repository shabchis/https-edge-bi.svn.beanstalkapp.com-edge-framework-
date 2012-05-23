using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Data.Pipeline.Services;
using Edge.Core.Utilities;

namespace Edge.Data.Pipeline.Services
{
	/// <summary>
	/// Retrieves all delivery files with a valid SourceUrl value.
	/// </summary>
	public class UrlRetrieverService: PipelineService
	{
		protected override Core.Services.ServiceOutcome DoPipelineWork()
		{
			// Create a batch and use its progress as the service's progress
			BatchDownloadOperation batch = new BatchDownloadOperation();
			batch.Progressed += new EventHandler((sender, e) =>
			{
				this.ReportProgress(batch.Progress * 0.99);
			});

			foreach (DeliveryFile file in this.Delivery.Files)
			{
				if (String.IsNullOrWhiteSpace(file.SourceUrl))
					continue;

				Log.Write(String.Format("Delivery file {0} starting download ({1}).",file.Name, file.FileID), LogMessageType.Information);

				DeliveryFileDownloadOperation download = file.Download();
				download.Ended += new EventHandler(download_Ended);
				batch.Add(download);
			}

			batch.Start();
			batch.Wait();

			// Add a retrieved history entry for the entire delivery
			this.Delivery.Save();

			return Core.Services.ServiceOutcome.Success;
		}

		void download_Ended(object sender, EventArgs e)
		{
			var operation = (DeliveryFileDownloadOperation)sender;
			operation.DeliveryFile.Status = DeliveryFileStatus.Retrieved;

			Log.Write(String.Format("Delivery file {0} retrieved successfully ({1}).", operation.DeliveryFile.Name, operation.DeliveryFile.FileID), LogMessageType.Information);
		}
	}
}
