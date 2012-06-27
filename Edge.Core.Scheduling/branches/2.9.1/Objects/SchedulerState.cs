using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Newtonsoft.Json;
using Edge.Core.Services;


namespace Edge.Core.Scheduling.Objects
{
	public class SchedulerState
	{
		public Dictionary<int, HistoryItem> HistoryItems = new Dictionary<int, HistoryItem>();
		private string _path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "schedulerHistory.json");
		public void Save()
		{

			JsonSerializerSettings settings = new JsonSerializerSettings();
			settings.TypeNameHandling = TypeNameHandling.All;
			settings.TypeNameAssemblyFormat = System.Runtime.Serialization.Formatters.FormatterAssemblyStyle.Full;
			JsonSerializer jsonSerializer = JsonSerializer.Create(settings);
			using (StreamWriter sw = new StreamWriter(_path, false, Encoding.Unicode))
			{
				JsonTextWriter writer = new JsonTextWriter(sw);

				jsonSerializer.Serialize(writer, HistoryItems);
			}
		}

		public void Load()
		{
			JsonSerializerSettings settings = new JsonSerializerSettings();
			settings.TypeNameHandling = TypeNameHandling.All;
			settings.TypeNameAssemblyFormat = System.Runtime.Serialization.Formatters.FormatterAssemblyStyle.Full;
			JsonSerializer jsonSerializer = JsonSerializer.Create(settings);
			if (File.Exists(_path))
			{
				using (StreamReader sr = new StreamReader(_path, Encoding.Unicode))
				{
					JsonTextReader reader = new JsonTextReader(sr);
					HistoryItems = jsonSerializer.Deserialize<Dictionary<int, HistoryItem>>(reader);

				}
			}

		}
	}
	public class HistoryItem
	{
		public int ID { get; set; }
		public Guid Guid { get; set; }
		public ServiceOutcome ServiceOutcome { get; set; }
		public string ServiceName { get; set; }
		public int AccountID { get; set; }
		public SchedulingResult SchedulingResult { get; set; }
		public TimeSpan MaxDeviationAfter { get; set; }
		public DateTime TimeToRun { get; set; }

		public HistoryItem()
		{

		}

		public static HistoryItem FromSchedulingData(SchedulingData data, ServiceInstance instance, SchedulingResult schedulingResult)
		{
			return new HistoryItem()
			{
				ID = data.GetHashCode(),
				ServiceName = instance.ServiceName,
				AccountID = data.ProfileID,
				SchedulingResult = schedulingResult,
				MaxDeviationAfter = data.Rule.MaxDeviationAfter,
				TimeToRun = data.TimeToRun,
				Guid = data.Guid,
				ServiceOutcome = instance.Outcome

			};

		}
	}
	public enum SchedulingResult
	{
		Ended,
		Canceled
	}

}
