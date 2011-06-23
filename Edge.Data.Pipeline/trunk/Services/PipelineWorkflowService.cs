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
		SettingsCollection _deliveryOptions;
		
		protected override void OnInit()
		{
			Guid deliveryID;
			string did;
			if (Instance.Configuration.Options.TryGetValue(Delivery.DeliveryIdOptionName, out did))
			{
				if (!Guid.TryParse(did, out deliveryID))
					throw new FormatException(String.Format("'{0}' is not a valid delivery GUID.", did));
			}
			else
				deliveryID = Guid.NewGuid();

			_deliveryOptions = new SettingsCollection()
			{
				{Delivery.DeliveryIdOptionName, deliveryID.ToString()}
			};
		}

		protected override void RequestChildService(int stepNumber, int attemptNumber, Core.SettingsCollection options = null)
		{
			if (options != null)
				options.Merge(_deliveryOptions);
			else
				options = _deliveryOptions;

			base.RequestChildService(stepNumber, attemptNumber, options);
		}
	}
}
