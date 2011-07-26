using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Data.Pipeline.Services;
using Edge.Data.Pipeline;
using Edge.Data.Objects;

namespace Edge.Data.Pipeline.Services
{
	public abstract class ValidationService: PipelineService
	{
		public static class Const
		{
			public static class HistoryParameters
			{
				public const string ValidationResult = "ValidationResult";
			}

			public static class ConfigurationOptions
			{ 
				public const string ShouldFail = "ShouldFail";
				public const string TableName = "TableName";
			}
		}

		public ValidationService()
		{
			//string shouldFail = this.Instance.Configuration.Options[Const.ConfigurationOptions.ShouldFail];
			//if (shouldFail != null)
			//    bool.TryParse(shouldFail, out _shouldFail);
		}

		bool _shouldFail = false;
		public virtual bool ShouldFailOnNoSuccess
		{
			get { return _shouldFail;}
		}

		protected override sealed Core.Services.ServiceOutcome DoPipelineWork()
		{

			string shouldFail = this.Instance.Configuration.Options[Const.ConfigurationOptions.ShouldFail];
			if (shouldFail != null)
				bool.TryParse(shouldFail, out _shouldFail);

			ValidationResult result = null;
			Exception exception = null;

			// Execute the validation
			try { result = this.Validate();}
			catch(Exception ex) { exception = ex;}

			if (result == null)
			{
				result = new ValidationResult()
				{
					Success = false,
					Message = exception != null ?
						String.Format("{0}: {1}", exception.GetType().Name, exception.Message) :
						"No validation result was returned."
				};
			}

			// Add the result to the delivery history
			this.Delivery.History.Add(DeliveryOperation.Validated, this.Instance.InstanceID, new Dictionary<string,object>(){
				{Const.HistoryParameters.ValidationResult,result}
			});
			this.Delivery.Save();

			// Report outcome
			if (this.ShouldFailOnNoSuccess && !result.Success)
			{
				if (exception == null)
					return Core.Services.ServiceOutcome.Failure;
				else
					throw exception;
			}
			else
			{
				return Core.Services.ServiceOutcome.Success;
			}
		}

		protected abstract ValidationResult Validate();
	}

	public class ValidationResult
	{
		public bool Success { get; set; }
		public string Message { get; set; }
		public Dictionary<string, object> Parameters { get; set; }
	}
}
