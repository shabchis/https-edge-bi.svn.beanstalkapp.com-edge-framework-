using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Objects
{
	public partial class CurrencyRate
	{
		public Currency Currency;
		public DateTime RateDate;
		public double RateValue;
		public DateTime DateCreated;
		//public DeliveryOutput Output;
	}

	public partial class Currency
	{
		public string Code;
	}
}
