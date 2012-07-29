using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Objects
{
	public class Currency
	{
		public string Code;
	}

	public class CurrencyRate
	{
		public Currency Currency;
		public DateTime RateDate;
		public double RateValue;
		public DateTime DateCreated;
	}
}
