using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;

namespace Edge.Data.Pipeline
{
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

		internal DeliveryFileDownloadOperation(DeliveryFile file, WebRequest request, string targetLocation, long length = -1)
			: base(request, targetLocation, length)
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
