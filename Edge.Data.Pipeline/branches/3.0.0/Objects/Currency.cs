using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Data.Pipeline;

namespace Edge.Data.Objects
{
	public class CurrencyRate
	{
		public Currency Currency;
		public DateTime RateDate;
		public double RateValue;
		public DateTime DateCreated;
		public DeliveryOutput Output;

		public CurrencyRate()
		{
			this.Currency = new Currency();
			this.DateCreated = DateTime.Today;
		}

		
	}
	
	public class Currency
	{
		public string Code;
	}

	
}
