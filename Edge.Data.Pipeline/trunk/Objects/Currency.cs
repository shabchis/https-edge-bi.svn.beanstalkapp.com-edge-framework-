using Edge.Core.Configuration;
using Edge.Core.Data;
using Edge.Core.Utilities;
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
        public Decimal RateValue;
        public DateTime DateCreated;

        public CurrencyRate()
        {
            this.Currency = new Currency();
            this.DateCreated = DateTime.Today;
        }

        public static Dictionary<string, CurrencyRate> GetCurrencyRates(DateTime dateTime)
        {
            using (SqlConnection connection = new SqlConnection(AppSettings.GetConnectionString(typeof(CurrencyRate), "CurrencyRateDatabase")))
            {
                connection.Open();
                SqlCommand cmd = DataManager.CreateCommand(AppSettings.Get(typeof(CurrencyRate), "SP.GetCurrencyRate"),
                    System.Data.CommandType.StoredProcedure);
                cmd.Connection = connection;

                cmd.Parameters["@Date"].Value = Convert.ToInt64(dateTime.ToString("yyyyMMdd")); ;

                List<CurrencyRate> currencyRates = new List<CurrencyRate>();

                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        CurrencyRate c = new CurrencyRate()
                        {
                            Currency = new Currency() { Code = Convert.ToString(reader["Code"]) },
                            RateValue = Convert.ToDecimal(reader["Rate"])
                        };

                        currencyRates.Add(c);
                    }
                }

                return currencyRates.ToDictionary(c => c.Currency.Code);
            }
        }

        public static void SaveCurrencyRates(List<CurrencyRate> Currencies)
        {
            using (SqlConnection connection = new SqlConnection(AppSettings.GetConnectionString(typeof(CurrencyRate), "CurrencyRateDatabase")))
            {
                connection.Open();
                SqlCommand cmd = DataManager.CreateCommand(AppSettings.Get(typeof(CurrencyRate), "SP.SaveCurrencyRates"), System.Data.CommandType.StoredProcedure);
                SqlTransaction transaction = connection.BeginTransaction("SaveCurrencyRates");

                try
                {
                    cmd.Connection = connection;
                    cmd.Transaction = transaction;

                    foreach (CurrencyRate currencyUnit in Currencies)
                    {
                        cmd.Parameters["@RateDate"].Value = Convert.ToInt64(currencyUnit.RateDate.ToString("yyyyMMdd"));
                        cmd.Parameters["@Rate"].Value = currencyUnit.RateValue;
                        cmd.Parameters["@Code"].Value = currencyUnit.Currency.Code;
                        cmd.ExecuteNonQuery();
                    }

                  transaction.Commit();
                }
                catch (Exception ex)
                {
                    Log.Write("Error while trying to save currencies in DB", ex);
                }
            }
        }
    }

    public class Currency
    {
        public string Code;
    }


}
