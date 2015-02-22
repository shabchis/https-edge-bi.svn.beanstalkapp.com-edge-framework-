using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Data;
using System.ComponentModel;
using System.Collections;

namespace Eggplant.Persistence.Validators
{
	// Eggplant-TODO:
	public class DateTimeValidator //: IValidator
	{
		public DateTime? _minDate, _maxDate;
		public DateTimeValidator(DateTime? MinDate, DateTime? MaxDate)
		{
			_minDate = MinDate;
			_maxDate = MaxDate;
		}

		public bool Validate(DateTime value)
		{
			return
				(_minDate == null || value >= _minDate.Value) &&
				(_maxDate == null || value <= _maxDate.Value);
		}
	}
}
