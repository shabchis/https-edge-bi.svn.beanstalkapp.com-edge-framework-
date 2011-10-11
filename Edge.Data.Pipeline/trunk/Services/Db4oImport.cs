using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Db4objects.Db4o.Config;
using Db4objects.Db4o;
using System.Data.SqlClient;
using Db4objects.Db4o.Linq;
using Edge.Core.Configuration;
namespace Edge.Data.Pipeline.Services
{
	class Db4oImport : PipelineService
	{
		protected override Core.Services.ServiceOutcome DoPipelineWork()
		{
			IEmbeddedConfiguration config = Db4oEmbedded.NewConfiguration();
			config.Common.ActivationDepth = 20;


			using (IObjectContainer db = Db4oEmbedded.OpenFile(config, this.Instance.Configuration.Options["DeliveryFilePath"]))
			{
				using (SqlConnection sqlConnection = new SqlConnection(AppSettings.GetConnectionString(this, "OLTP")))
				{
					using (SqlCommand cmd = new SqlCommand(@"select distinct  deliveryID FROM dbo.Paid_API_AllColumns_v29
															 where day_code between @from and @to "))
					{
						cmd.Parameters.AddWithValue("@from",int.Parse(this.Instance.Configuration.Options["From"]));
						cmd.Parameters.AddWithValue("@from",int.Parse(this.Instance.Configuration.Options["Value"]));
						cmd.CommandTimeout = 0;
						sqlConnection.Open();
						cmd.Connection = sqlConnection;
						using (SqlDataReader reader = cmd.ExecuteReader())
						{
							while (reader.Read())
							{
								Guid g=Guid.Parse(reader.GetString(0));
								var result = from Delivery d in db
											 where d.DeliveryID == g
											 select d;
								try
								{
									if (result.Count()>0)
									{
										result.Last().IsCommited = true;
										DeliveryDB.Save(result.Last());
									}

								}
								catch (Exception)
								{
									
									
								}
																	
								
							}

						}
					}



				}

			}

			return Core.Services.ServiceOutcome.Success;

		}
	}
}
