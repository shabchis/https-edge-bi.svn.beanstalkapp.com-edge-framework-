using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Data.Pipeline.Services;

namespace Edge.Data.Pipeline.Services
{
	/// <summary>
	/// Retrieves all delivery files with a valid SourceUrl value.
	/// </summary>
	public class GenericRetrieverService: PipelineService
	{
		protected override Core.Services.ServiceOutcome DoPipelineWork()
		{
			// Create a batch and use its progress as the service's progress
			BatchDownloadOperation batch = new BatchDownloadOperation();
			batch.Progressed += new EventHandler((sender, e) =>
			{
				this.ReportProgress(batch.Progress * 0.95);
			});

			foreach (DeliveryFile file in this.Delivery.Files)
			{
				if (String.IsNullOrWhiteSpace(file.SourceUrl))
					continue;

				DeliveryFileDownloadOperation download = file.Download();
				download.Ended += new EventHandler(download_Ended);
				batch.Add(download);
			}

			batch.Start();
			batch.Wait();

			// Add a retrieved history entry for the entire delivery
			this.Delivery.History.Add(DeliveryOperation.Retrieved, this.Instance.InstanceID);
			this.Delivery.Save();

			return Core.Services.ServiceOutcome.Success;
		}

		void download_Ended(object sender, EventArgs e)
		{
			// Add a retrieved history entry to every file
			((DeliveryFileDownloadOperation)sender).DeliveryFile.History.Add(DeliveryOperation.Retrieved, this.Instance.InstanceID);
		}
	}
}
