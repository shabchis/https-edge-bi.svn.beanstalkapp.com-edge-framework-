using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using System.Text;
using Edge.Core.Configuration;
using Edge.Core.Services;
using Edge.Core.Utilities;
using Edge.Data.Objects;
using Edge.Data.Pipeline;


namespace Edge.Data.Pipeline.Metrics.AdMetrics
{
	/// <summary>
	/// Encapsulates the process of adding ads and ad metrics to the delivery staging database.
	/// </summary>
	public class AdMetricsImportManager : MetricsDeliveryManager<AdMetricsUnit>
	{
		
		/// <summary>
		/// 
		/// </summary>
		public AdMetricsImportManager(long serviceInstanceID, MetricsDeliveryManagerOptions options = null)
			: base(serviceInstanceID, options)
		{
			bool hasMeasureOptions = this.Options.MeasureOptions != MeasureOptions.None;
			this.Options.MeasureOptions = hasMeasureOptions ? this.Options.MeasureOptions : MeasureOptions.All;
			this.Options.MeasureOptionsMatch = hasMeasureOptions ? this.Options.MeasureOptionsMatch : OptionsMatching.Any;

			bool hasSegmentOptions = this.Options.MetaPropertyOptions != MetaPropertyOptions.None;
			this.Options.MetaPropertyOptions = hasSegmentOptions ? this.Options.MetaPropertyOptions : MetaPropertyOptions.All;
			this.Options.MetaPropertyOptionsMatch = hasSegmentOptions ? this.Options.MetaPropertyOptionsMatch : OptionsMatching.Any;
		}

		/// <summary>
		/// Imports an ad into the delivery's staging tables.
		/// </summary>
		public void ImportAd(Ad ad)
		{
			EnsureBeginImport();

			string adUsid = GetAdIdentity(ad);

			// Ad
			var adRow = new Dictionary<ColumnDef, object>()
			{
				{Tables.Ad.AdUsid, adUsid},
				{Tables.Ad.Channel_ID, ad.Channel == null ? -1 : ad.Channel.ID },
				{Tables.Ad.Account_ID, ad.Account == null ? -1 : ad.Account.ID },
				{Tables.Ad.Account_OriginalID, ad.Account.OriginalID},
				{Tables.Ad.Name, ad.Name},
				{Tables.Ad.OriginalID, ad.OriginalID},
				{Tables.Ad.DestinationUrl, ad.DestinationUrl},
				{Tables.Ad.AdStatus, ad.Status}
			};
			foreach (KeyValuePair<ExtraField, object> extraField in ad.ExtraFields)
				adRow[new ColumnDef(Tables.AdTarget.ExtraFieldX, extraField.Key.ColumnIndex)] = extraField.Value;

			Bulk<Tables.Ad>().SubmitRow(adRow);

			// AdTarget
			foreach (Target target in ad.Targets)
			{
				var row = new Dictionary<ColumnDef, object>()
				{
					{ Tables.AdTarget.AdUsid, adUsid },
					{ Tables.AdTarget.TypeID, target.TypeID },
					{ Tables.AdTarget.OriginalID, target.OriginalID },
					{ Tables.AdTarget.Status, target.Status == ObjectStatus.Unknown ? (object)DBNull.Value :  target.Status },
					{ Tables.AdTarget.DestinationUrl, target.DestinationUrl }
				};

				foreach (KeyValuePair<MappedObjectField, object> fixedField in target.GetFieldValues())
					row[new ColumnDef(Tables.AdTarget.FieldX, fixedField.Key.ColumnIndex)] = fixedField.Value;

				foreach (KeyValuePair<ExtraField, object> extraField in target.ExtraFields)
					row[new ColumnDef(Tables.AdTarget.ExtraFieldX, extraField.Key.ColumnIndex)] = extraField.Value;

				Bulk<Tables.AdTarget>().SubmitRow(row);
			}

			// AdCreative
			foreach (Creative creative in ad.Creatives)
			{
				var row = new Dictionary<ColumnDef, object>()
				{
					{ Tables.AdCreative.AdUsid, adUsid },
					{ Tables.AdCreative.OriginalID, creative.OriginalID },
					{ Tables.AdCreative.Name, creative.Name },
					{ Tables.AdCreative.CreativeType, creative.TypeID }
				};

				foreach (KeyValuePair<MappedObjectField, object> fixedField in creative.GetFieldValues())
					row[new ColumnDef(Tables.AdCreative.FieldX, fixedField.Key.ColumnIndex)] = fixedField.Value;

				foreach (KeyValuePair<ExtraField, object> extraField in creative.ExtraFields)
					row[new ColumnDef(Tables.AdCreative.ExtraFieldX, extraField.Key.ColumnIndex)] = extraField.Value;

				Bulk<Tables.AdCreative>().SubmitRow(row);
			}

			// AdSegment
			foreach (var segment in ad.Segments)
			{
				var row = new Dictionary<ColumnDef, object>()
				{
					{ Tables.AdSegment.AdUsid, adUsid },
					{ Tables.AdSegment.SegmentID, segment.Key.ID},
					{ Tables.AdSegment.TypeID, segment.Value.TypeID },
					{ Tables.AdSegment.OriginalID, segment.Value.OriginalID },
					{ Tables.AdSegment.Status, segment.Value.Status == ObjectStatus.Unknown ? (object)DBNull.Value :  segment.Value.Status},
					{ Tables.AdSegment.Value, segment.Value.Value }
					
					
				};

				foreach (KeyValuePair<MappedObjectField, object> fixedField in segment.Value.GetFieldValues())
					row[new ColumnDef(Tables.AdSegment.FieldX, fixedField.Key.ColumnIndex)] = fixedField.Value;

				foreach (KeyValuePair<ExtraField, object> extraField in segment.Value.ExtraFields)
					row[new ColumnDef(Tables.AdSegment.ExtraFieldX, extraField.Key.ColumnIndex)] = extraField.Value;

				Bulk<Tables.AdSegment>().SubmitRow(row);
			}
		}

		/// <summary>
		/// Imports a metrics unit into the delivery's staging tables.
		/// </summary>
		public override void ImportMetrics(AdMetricsUnit metrics)
		{
			EnsureBeginImport();

			if (metrics.Output == null)
				throw new InvalidOperationException("Cannot import a metrics unit that is not associated with a delivery output.");
			if (metrics.Ad == null)
				throw new InvalidOperationException("Cannot import a metrics unit that is not associated with an ad.");

			string adUsid = GetAdIdentity(metrics.Ad);
			string metricsUsid = metrics.Usid.ToString("N");


			// Metrics
			var metricsRow = new Dictionary<ColumnDef, object>()
			{
				{Tables.Metrics.MetricsUsid, metricsUsid},
				{Tables.Metrics.OutputID,metrics.Output.OutputID.ToString("N")},
				{Tables.Metrics.AdUsid, adUsid},
				{Tables.Metrics.TargetPeriodStart, metrics.Output.TimePeriodStart},
				{Tables.Metrics.TargetPeriodEnd, metrics.Output.TimePeriodEnd},
				{Tables.Metrics.Currency, metrics.Currency == null ? null : metrics.Currency.Code}
			};

			foreach (KeyValuePair<Measure, double> measure in metrics.MeasureValues)
			{
				metricsRow[new ColumnDef(measure.Key.Name)] = measure.Value;

				////TO DO : If "Currency to USD is checked" Add new column "***_USD", and value in USD by CLR
				//if (measure.Key.IsCurrency && metrics.Currency.Code != "USD")
				//{
				//    using (SqlConnection oltpConnection = new SqlConnection(this.Options.StagingConnectionString))
				//    {
				//        metricsRow[new ColumnDef(measure.Key.Name + "_USD")] = measure.Key.GetValueInUSD(oltpConnection, measure.Value);
				//    }
				//}
			}

			Bulk<Tables.Metrics>().SubmitRow(metricsRow);

			// MetricsDimensionTarget
			if (metrics.TargetDimensions != null)
			{
				foreach (Target target in metrics.TargetDimensions)
				{
					var row = new Dictionary<ColumnDef, object>()
				{
					{ Tables.MetricsDimensionTarget.MetricsUsid, metrics.Usid.ToString("N") },
                    { Tables.MetricsDimensionTarget.AdUsid, adUsid},
					{ Tables.MetricsDimensionTarget.TypeID, target.TypeID },
					{ Tables.MetricsDimensionTarget.OriginalID, target.OriginalID },
					{ Tables.MetricsDimensionTarget.Status, target.Status == ObjectStatus.Unknown ? (object)DBNull.Value :  target.Status },
					{ Tables.MetricsDimensionTarget.DestinationUrl, target.DestinationUrl }
				};

					foreach (KeyValuePair<MappedObjectField, object> fixedField in target.GetFieldValues())
						row[new ColumnDef(Tables.MetricsDimensionTarget.FieldX, fixedField.Key.ColumnIndex)] = fixedField.Value;

					foreach (KeyValuePair<ExtraField, object> customField in target.ExtraFields)
						row[new ColumnDef(Tables.MetricsDimensionTarget.ExtraFieldX, customField.Key.ColumnIndex)] = customField.Value;

					Bulk<Tables.MetricsDimensionTarget>().SubmitRow(row);
				}
			}

		}

		protected override string TablePrefix
		{
			get { return "AD"; }
		}

		protected override Type MetricsTableDefinition
		{
			get { return typeof(Tables.Metrics); }
		}

		protected override void OnStage(Delivery delivery, int pass)
		{
			base.OnStage(delivery, pass);
		}

		protected override void OnTransform(Delivery delivery, int pass)
		{
			base.OnTransform(delivery, pass);
		}
	}
}
