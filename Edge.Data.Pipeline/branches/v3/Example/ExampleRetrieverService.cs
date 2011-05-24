using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Easynet.Edge.Core.Services;
using Easynet.Edge.Core;
using Easynet.Edge.Core.Data;

namespace Services.Data.Pipeline
{
	public class ExampleRetrieverService : Service
	{
		protected override ServiceOutcome DoWork()
		{
			// Create a new delivery with a description
			Delivery delivery = new Delivery();

			// Assign this delivery to an account/client/scope
			delivery.AccountID = Instance.AccountID;
			
			// Get the target datetime from the configuration
			delivery.TargetDateTime = DateTime.Parse(Instance.Configuration.Options["DateTime"]).ToLocalTime();

			// Add files
			delivery.Files.Add(new DeliveryFile
			{
				Url = "http://wwww.chunk.com?file=1",
				ReaderType = typeof(ExampleReader), // this is optional, depending on processor type
				Parameters = new SettingsCollection() // parameters for the reader
				{
					{"FilterCampaigns", "Casino*"},
					{"UseAccoutHeader", true}
				}
			});
			delivery.Files.Add(new DeliveryFile
			{
				Url = "http://wwww.chunk.com?file=1",
				ReaderType = typeof(ExampleReader), // this is optional, depending on processor type
				Parameters = new Settings() // parameters for the reader
				{
					{"FilterCampaigns", "Poker*"},
					{"UseAccoutHeader", true}
				}
			});
			delivery.Files.Add(new DeliveryFile
			{
				Url = "http://wwww.chunk.com?file=2",
				ReaderType = typeof(ExampleReader)
			});

			// Save settings to DB
			//delivery.Save();

			// Download and report download progress
            delivery.Retrieve();//p => ReportProgress(p));

			// Remember this delivery for the processor
			// TODO: implement ServiceContext in Edge.Core.Service
			ServiceContext["DeliveryID"] = delivery.ID;

			return ServiceOutcome.Success;
		}
	}
}
