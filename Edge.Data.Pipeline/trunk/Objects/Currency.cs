using Edge.Core.Configuration;
using Edge.Core.Data;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;

namespace Edge.Data.Objects
{
	public class CurrencyRate
	{
		public Currency Currency;
		public DateTime RateDate;
		public double RateValue;
		public DateTime DateCreated;

		public CurrencyRate()
		{
			this.Currency = new Currency();
			this.DateCreated = DateTime.Today;
		}

        public static Dictionary<string, CurrencyRate> GetCurrencyRates(DateTime dateTime)
        {
            SqlConnection connection = new SqlConnection(AppSettings.GetConnectionString(typeof(CurrencyRate), "CurrencyRateDatabase"));
            SqlCommand cmd = DataManager.CreateCommand(AppSettings.Get(typeof(CurrencyRate), "GetCurrencyRate.SP"),
                System.Data.CommandType.StoredProcedure);
            cmd.Connection = connection;

            cmd.Parameters["@Date"].Value = dateTime;

            List<CurrencyRate> currencyRates = new List<CurrencyRate>();
            
            using (SqlDataReader reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    CurrencyRate c = new CurrencyRate()
                    {
                        Currency = new Currency(){Code=(string)reader["Code"]},
                        RateValue = (double)reader["Rate"],
                    };

                    currencyRates.Add(c);
                }
            }

            return currencyRates.ToDictionary(c=>c.Currency.Code);
        }
    }
	
	public class Currency
	{
		public string Code;
	}

	
}
