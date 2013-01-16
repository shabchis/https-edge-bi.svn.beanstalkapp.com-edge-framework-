using System;
using Edge.Data.Objects;
using Edge.Data.Pipeline.Metrics.Base;
using Edge.Data.Pipeline.Objects;

namespace Edge.Data.Pipeline.Metrics.Implementation
{
	//public class GenericMetricsImportManager : MetricsDeliveryManager<GenericMetricsUnit>
	//{
	//	public GenericMetricsImportManager(Guid serviceInstanceID, MetricsDeliveryManagerOptions options = null)
	//		: base(serviceInstanceID, options)
	//	{
	//		var hasMeasureOptions = Options.MeasureOptions != MeasureOptions.None;
	//		//Options.MeasureOptions = hasMeasureOptions ? Options.MeasureOptions :MeasureOptions.IsBackOffice;
	//		Options.MeasureOptionsMatch = hasMeasureOptions ? Options.MeasureOptionsMatch : OptionsMatching.Any;

	//		//bool hasSegmentOptions = this.Options.SegmentOptions != SegmentOptions.None;
	//		//this.Options.SegmentOptions = hasSegmentOptions ? this.Options.SegmentOptions : Data.Objects.SegmentOptions.All;
	//		//this.Options.SegmentOptionsOperator = hasSegmentOptions ? this.Options.SegmentOptionsOperator : OptionsMatching.All;
	//	}

	//	//public override void ImportMetrics(GenericMetricsUnit metrics)
	//	//{
	//	//}

	//	//public override void ImportMetrics(GenericMetricsUnit metrics)
	//	//{
	//	//	if (metrics.Output == null)
	//	//		throw new InvalidOperationException("Cannot import a metrics unit that is not associated with a delivery output.");

	//	//	metrics.Account = metrics.Output.Account;
	//	//	metrics.Channel = metrics.Output.Channel;
	//	//	metrics.TimePeriodStart = metrics.Output.TimePeriodStart;
	//	//	metrics.TimePeriodEnd = metrics.Output.TimePeriodEnd;

	//	//	// Metrics
	//	//	var metricsRow = new Dictionary<ColumnDef, object>()
	//	//	{
	//	//		{Tables.Metrics.MetricsUsid, metrics.Usid.ToString("N")},
	//	//		{Tables.Metrics.OutputID,metrics.Output.OutputID.ToString("N")},
	//	//		{Tables.Metrics.TargetPeriodStart, metrics.TimePeriodStart},
	//	//		{Tables.Metrics.TargetPeriodEnd, metrics.TimePeriodEnd},
	//	//		{Tables.Metrics.Account_ID, metrics.Account.ID},
	//	//		{Tables.Metrics.Account_OriginalID, metrics.Account.OriginalID == null ? (object)DBNull.Value : metrics.Account.OriginalID },
	//	//		{Tables.Metrics.Channel_ID, metrics.Channel.ID}
	//	//	};

	//	//	foreach (KeyValuePair<Measure, double> measure in metrics.MeasureValues)
	//	//	{
	//	//		// Use the Oltp name of the measure as the column name
	//	//		metricsRow[new ColumnDef(measure.Key.Name)] = measure.Value;
	//	//	}
	//	//	Bulk<Tables.Metrics>().SubmitRow(metricsRow);

	//	//	// MetricsDimensionSegment
	//	//	foreach (var segment in metrics.SegmentDimensions)
	//	//	{
	//	//		var row = new Dictionary<ColumnDef, object>()
	//	//		{
	//	//			{ Tables.MetricsDimensionSegment.MetricsUsid, metrics.Usid.ToString("N") },
	//	//			{ Tables.MetricsDimensionSegment.SegmentID, segment.Key.ID},
	//	//			{ Tables.MetricsDimensionSegment.TypeID, segment.Value.TypeID },
	//	//			{ Tables.MetricsDimensionSegment.OriginalID, segment.Value.OriginalID },
	//	//			{ Tables.MetricsDimensionSegment.Status, segment.Value.Status == ObjectStatus.Unknown ? (object)DBNull.Value :  segment.Value.Status},
	//	//			{ Tables.MetricsDimensionSegment.Value, segment.Value.Value }
	//	//		};

	//	//		foreach (KeyValuePair<MappedObjectField, object> fixedField in segment.Value.GetFieldValues())
	//	//			row[new ColumnDef(Tables.MetricsDimensionTarget.FieldX, fixedField.Key.ColumnIndex)] = fixedField.Value;

	//	//		foreach (KeyValuePair<ExtraField, object> customField in segment.Value.ExtraFields)
	//	//			row[new ColumnDef(Tables.MetricsDimensionTarget.ExtraFieldX, customField.Key.ColumnIndex)] = customField.Value;

	//	//		Bulk<Tables.MetricsDimensionSegment>().SubmitRow(row);
	//	//	}

	//	//	// MetricsDimensionTarget
	//	//	if (metrics.TargetDimensions != null)
	//	//	{
	//	//		foreach (Target target in metrics.TargetDimensions)
	//	//		{
	//	//			var row = new Dictionary<ColumnDef, object>()
	//	//		{
	//	//			{ Tables.MetricsDimensionTarget.MetricsUsid, metrics.Usid.ToString("N") },
	//	//			{ Tables.MetricsDimensionTarget.TypeID, target.TypeID },
	//	//			{ Tables.MetricsDimensionTarget.OriginalID, target.OriginalID },
	//	//			{ Tables.MetricsDimensionTarget.Status, target.Status == ObjectStatus.Unknown ? (object)DBNull.Value :  target.Status },
	//	//			{ Tables.MetricsDimensionTarget.DestinationUrl, target.DestinationUrl }
	//	//		};

	//	//			foreach (KeyValuePair<MappedObjectField, object> fixedField in target.GetFieldValues())
	//	//				row[new ColumnDef(Tables.MetricsDimensionTarget.FieldX, fixedField.Key.ColumnIndex)] = fixedField.Value;

	//	//			foreach (KeyValuePair<ExtraField, object> customField in target.ExtraFields)
	//	//				row[new ColumnDef(Tables.MetricsDimensionTarget.ExtraFieldX, customField.Key.ColumnIndex)] = customField.Value;

	//	//			Bulk<Tables.MetricsDimensionTarget>().SubmitRow(row);
	//	//		}
	//	//	}
	//	//}

	//	protected override void OnCommit(Delivery delivery, int pass)
	//	{
	//		throw new NotImplementedException();
	//	}
	//}
}
