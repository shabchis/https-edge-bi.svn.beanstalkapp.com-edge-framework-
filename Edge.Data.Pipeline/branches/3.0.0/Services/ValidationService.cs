using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Core.Utilities;
using Edge.Data.Pipeline.Services;
using Edge.Data.Pipeline;
using Edge.Data.Objects;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Edge.Core.Services;

namespace Edge.Data.Pipeline.Services
{
	public abstract class ValidationService : PipelineService
	{
		public static class Const
		{
			public static class ConfigurationOptions
			{
				public const string FailureLevel = "FailureLevel";
			}
		}

		public ValidationResultType FailureLevel
		{
			get;
			private set;
		}

		protected override sealed Core.Services.ServiceOutcome DoPipelineWork()
		{
			this.FailureLevel = this.Configuration.Parameters.Get<ValidationResultType>(Const.ConfigurationOptions.FailureLevel, false, ValidationResultType.Error);

			var entries = new List<ValidationResult>();
			ValidationResultType maxLevel = ValidationResultType.Information;

			// Execute the validation
			try
			{
				foreach (ValidationResult entry in this.Validate())
				{
					entries.Add(entry);
					if ((int)entry.ResultType < (int)maxLevel)
						maxLevel = entry.ResultType;
				}
			}
			catch (Exception ex)
			{
				entries.Add(new ValidationResult()
				{
					ResultType = ValidationResultType.Error, 
					Message = "Exception occured during validation.",
					Exception = ex
				});
			}

			foreach (ValidationResult entry in entries)
				entry.LogWrite();

			//if (this.FailureLevel != ValidationResultType.None && maxLevel <= this.FailureLevel)
			//    return Core.Services.ServiceOutcome.Failure;
			//else
			// Removed by Shay 09.11.11 ( Error is not Failure )


				return Core.Services.ServiceOutcome.Success;
		}

		protected abstract IEnumerable<ValidationResult> Validate();
	}


	public class ValidationResult
	{
		public ValidationResultType ResultType { get; set; }
		public string Message { get; set; }
        public int AccountID { get; set; }
        public int ChannelID { get; set; }
        public string CheckType { get; set; }
		public Guid DeliveryID { get; set; }
		public DateTime TargetPeriodStart { get; set; }
		public DateTime TargetPeriodEnd { get; set; }
		public Exception Exception { get; set; }

		internal void LogWrite()
		{
            string jsonMessage = JsonConvert.SerializeObject(this);
            Service.Current.Log(jsonMessage, this.Exception,
				this.ResultType == ValidationResultType.Error ? LogMessageType.Error :
				this.ResultType == ValidationResultType.Information ? LogMessageType.Information :
				this.ResultType == ValidationResultType.Warning ? LogMessageType.Warning :
				LogMessageType.Verbose
			);
		}
	}

	public enum ValidationResultType
	{
		None = 0,
		Error = 1,
		Warning = 2,
		Information = 3
	}
    
}
