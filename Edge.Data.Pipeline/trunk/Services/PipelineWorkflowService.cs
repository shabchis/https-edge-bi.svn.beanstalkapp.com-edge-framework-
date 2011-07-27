using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Core.Services;
using Edge.Core;

namespace Edge.Data.Pipeline.Services
{
	public class PipelineWorkflowService: Service
	{	
		protected override void OnInit()
		{
			Guid deliveryID;
			string did;
			if (Instance.Configuration.Options.TryGetValue(PipelineService.ConfigurationOptionNames.DeliveryID, out did))
			{
				if (!Guid.TryParse(did, out deliveryID))
					throw new FormatException(String.Format("'{0}' is not a valid delivery GUID.", did));
			}
			else
				deliveryID = Guid.NewGuid();

			this.Instance.Configuration.Options.Add(PipelineService.ConfigurationOptionNames.DeliveryID, deliveryID.ToString());
		}

		protected override void RequestChildService(int stepNumber, int attemptNumber, Core.SettingsCollection options = null)
		{
			if (options != null)
				options.Merge(this.Instance.Configuration.Options);
			else
				options = this.Instance.Configuration.Options;

			base.RequestChildService(stepNumber, attemptNumber, options);
		}
	}
}
