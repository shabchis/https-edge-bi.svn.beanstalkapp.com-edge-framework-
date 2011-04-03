using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;

namespace Edge.Data.Pipeline
{
	public struct DateTimeRange
	{
		public DateTimeSpecification Start;
		public DateTimeSpecification End;

		public DateTimeRange(DateTimeSpecification singleUnit)
		{
			Start = singleUnit;
			Start.Limit = false;
			End = singleUnit;
			End.Limit = true;
		}

		public static DateTimeRange Parse(string json)
		{
			throw new NotImplementedException();
		}
	}

	public struct DateTimeSpecification
	{
		public bool Limit;
		public DateTime ExactDateTime;
		public TimeSpan ExactTime;
		public DayOfWeek FirstDayOfWeek;

		public DateTimeTransformation Year;
		public DateTimeTransformation Month;
		public DateTimeTransformation Week;
		public DateTimeTransformation Day;
		public DateTimeTransformation Hour;
		public DateTimeTransformation Minute;
		public DateTimeTransformation Second;

		public DateTime ToDateTime()
		{
			if (ExactDateTime != DateTime.MinValue)
				return ExactDateTime;

			DateTime date = DateTime.Now;

			if (Year.Limit)
				throw new FormatException("Cannot set year to 'limit'.");

			bool limit = this.Limit;

			//...........................
			// Year
			date = Year.Type == DateTimeTransformationType.Exact ?
				(Limit ?
					new DateTime(Year.Value, 1, 1).AddYears(1).AddTicks(-1):
					new DateTime(Year.Value, 1, 1)
				):
				date.AddYears(Year.Value);

			//...........................
			// Month
			limit |= Month.Limit;
			date = Month.Type == DateTimeTransformationType.Exact ?
				(Limit ?
					new DateTime(date.Year, Month.Value, 1).AddMonths(1).AddTicks(-1):
					new DateTime(date.Year, Month.Value, 1)
				):
				date.AddMonths(Month.Value);


			//...........................
			// Week
			bool weekSpecified = Week.Value != 0 || Week.Limit;
			if (weekSpecified)
			{
				throw new NotImplementedException("Week not yet supported.");
				limit |= Week.Limit;
				date = Week.Type == DateTimeTransformationType.Exact ?
					(Limit ?
						new DateTime(date.Year, Month.Value, 1).SetWeekOfMonth(Week.Value).AddWeekOfMonth(1).AddTicks(-1) :
						new DateTime(date.Year, Month.Value, 1).SetWeekOfMonth(Week.Value)
					) :
					date.AddDays(Week.Value * 7);
			}

			//...........................
			// Day
			if (weekSpecified)
			{
				throw new NotImplementedException("Week not yet supported.");
			}
			else
			{
				limit |= Day.Limit;
				date = Day.Type == DateTimeTransformationType.Exact ?
					(Limit ?
						new DateTime(date.Year, Month.Value, Day.Value).AddDays(1).AddTicks(-1) :
						new DateTime(date.Year, Month.Value, Day.Value)
					) :
					date.AddDays(Day.Value);
			}

			// Time
			if (ExactTime != TimeSpan.Zero)
				date = new DateTime(date.Year, date.Month, date.Day).Add(ExactTime);

			// Hour
			limit |= Hour.Limit;
			date = Hour.Type == DateTimeTransformationType.Exact ?
				(Limit ?
					new DateTime(date.Year, date.Month, date.Day, Hour.Value, 0, 0).AddHours(1).AddTicks(-1) :
					new DateTime(date.Year, date.Month, date.Day, Hour.Value, 0, 0)
				) :
				date.AddHours(Hour.Value);

			// Minute
			limit |= Minute.Limit;
			date = Minute.Type == DateTimeTransformationType.Exact ?
				(Limit ?
					new DateTime(date.Year, date.Month, date.Day, date.Hour, Minute.Value, 0).AddMinutes(1).AddTicks(-1) :
					new DateTime(date.Year, date.Month, date.Day, date.Hour, Minute.Value, 0)
				) :
				date.AddMinutes(Minute.Value);

			// Second
			limit |= Second.Limit;
			date = Second.Type == DateTimeTransformationType.Exact ?
				(Limit ?
					new DateTime(date.Year, date.Month, date.Day, date.Hour, date.Month, Second.Value).AddSeconds(1).AddTicks(-1) :
					new DateTime(date.Year, date.Month, date.Day, date.Hour, date.Month, Second.Value)
				) :
				date.AddSeconds(Second.Value);

			return date;
		}

		public static DateTimeRange Parse(string json)
		{
			throw new NotImplementedException();
		}
	}

	public enum DateTimeLimit
	{
		Lower,
		Upper
	}

	public struct DateTimeTransformation
	{
		public DateTimeTransformationType Type;
		public int Value;
		public bool Limit;
		public static readonly DateTimeTransformation Empty = new DateTimeTransformation();

		public override int GetHashCode()
		{
			unchecked
			{
				return Type.GetHashCode() ^ Value.GetHashCode() ^ Limit.GetHashCode();
			}
		}

		public override bool Equals(object obj)
		{
			if (!(obj is DateTimeTransformation))
				return false;

			var tf = (DateTimeTransformation)obj;
			return tf.Type == this.Type && tf.Value == this.Value && tf.Limit == this.Limit;
		}

		public override string ToString()
		{
			return String.Format("{0}{1}",
				this.Type == DateTimeTransformationType.Relative && this.Value != 0 ?
					(Value > 0 ? "+" : "-") :
					string.Empty,
				this.Type == DateTimeTransformationType.Exact && this.Limit ?
					"'limit'" :
					this.Value.ToString()
				);
		}

		public static DateTimeRange Parse(string json)
		{
			throw new NotImplementedException();
		}
	}

	public enum DateTimeTransformationType
	{
		Relative,
		Exact
	}

	public static class DateTimeExtensions
	{
		static GregorianCalendar _gc = new GregorianCalendar();
		public static int GetWeekOfMonth(this DateTime time, DayOfWeek firstDayOfWeek = DayOfWeek.Monday)
		{
			DateTime first = new DateTime(time.Year, time.Month, 1);
			return time.GetWeekOfYear(firstDayOfWeek) - first.GetWeekOfYear(firstDayOfWeek) + 1;
		}

		public static DateTime AddWeekOfMonth(this DateTime time, int weeks, DayOfWeek firstDayOfWeek = DayOfWeek.Monday)
		{
			throw new NotImplementedException();
		}

		public static DateTime SetWeekOfMonth(this DateTime time, int week, DayOfWeek firstDayOfWeek = DayOfWeek.Monday)
		{
			//...........................
			// Week -- last = 4th Monday
			/*
			bool weekIsSpecified = Week.Value != 0 || Week.Last;
			if (weekIsSpecified)
			{
				// calculate first monday of month
				int day;
				if (Week.Type == DateTimeTransformationType.Exact)
				{
					DateTime temp = new DateTime(date.Year, date.Month, 1);
					int firstWeekDayOfMonth = (int)temp.DayOfWeek - (int)this.FirstDayOfWeek;
					if (firstWeekDayOfMonth < 0)
						firstWeekDayOfMonth += 7;
					int firstFullWeekStartsOn = firstWeekDayOfMonth == 0 ? 1 : 8 - firstWeekDayOfMonth;

					last |= Week.Last;
				}

				date = Week.Type == DateTimeTransformationType.Exact ?
					new DateTime(date.Year, date.Month, Week) :
					date.AddDays(Week.Value * 7);
			}
			*/
			
			throw new NotImplementedException();
		}

		static int GetWeekOfYear(this DateTime time, DayOfWeek firstDayOfWeek = DayOfWeek.Monday)
		{
			return _gc.GetWeekOfYear(time, CalendarWeekRule.FirstDay, firstDayOfWeek);
		}
	}

	public enum WeekOfMonth
	{
		First = 1,
		Second = 2,
		Third = 3,
		Fourth = 4,
		Fifth = 5
	}

}
