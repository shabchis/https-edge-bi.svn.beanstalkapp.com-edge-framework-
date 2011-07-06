using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Pipeline
{
	public class Consts
	{
		public static class DeliveryHistoryParameters
		{
			public const string TablePerfix = "TablePerfix";
			public const string MeasureNamesSql = "MeasureNamesSql";
			public const string MeasureOltpFieldsSql = "MeasureOltpFieldsSql";
		}

		public static class AppSettings
		{
			public const string Delivery_SqlDb = "Sql.DeliveriesDb";
			public const string Delivery_RollbackCommand = "Sql.RollbackSqlCommand";
		}
	}
}
