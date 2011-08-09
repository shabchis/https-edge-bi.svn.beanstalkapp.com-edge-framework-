using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Data.Pipeline.Services;
using Edge.Data.Pipeline;
using Edge.Data.Objects;
using Edge.Core.Utilities;

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
			string failureLevelRaw = this.Instance.Configuration.Options[Const.ConfigurationOptions.FailureLevel];
			ValidationResultType failureLevel = ValidationResultType.Error;
			if (failureLevelRaw != null)
				Enum.TryParse<ValidationResultType>(failureLevelRaw, out failureLevel);
			this.FailureLevel = failureLevel;

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

			if (this.FailureLevel != ValidationResultType.None && maxLevel <= this.FailureLevel)
				return Core.Services.ServiceOutcome.Failure;
			else
				return Core.Services.ServiceOutcome.Success;
		}

		protected abstract IEnumerable<ValidationResult> Validate();
	}


	public class ValidationResult
	{
		public ValidationResultType ResultType { get; set; }
		public string Message { get; set; }
		public int AccountID { get; set; }
		public Guid DeliveryID { get; set; }
		public DateTime TargetPeriodStart { get; set; }
		public DateTime TargetPeriodEnd { get; set; }
		public Exception Exception { get; set; }

		internal void LogWrite()
		{
			string message = String.Format("Validation result: {0}{1}",

				// {0}
				this.Message == null ?
					null :
					this.Message + string.Empty,

				// {1}
				this.TargetPeriodStart == DateTime.MinValue && this.TargetPeriodEnd == DateTime.MinValue && this.DeliveryID == Guid.Empty ?
					null :
					String.Format("({0}{1})",
						// {0}
						this.TargetPeriodStart != DateTime.MinValue || this.TargetPeriodEnd != DateTime.MinValue ?
							String.Format("period: {0:yyyy-MM-dd}-{1:yyyy-MM-dd}", this.TargetPeriodStart, this.TargetPeriodEnd) :
							null,
						// {1}
						this.DeliveryID != Guid.Empty ?
							String.Format("delivery: {0}", this.DeliveryID) :
							null
					)
			);

			Log.Write(message, this.Exception, (LogMessageType)(int)this.ResultType);

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
