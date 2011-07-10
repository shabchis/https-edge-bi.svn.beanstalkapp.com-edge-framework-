using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Dynamic;
using Newtonsoft.Json.Linq;

namespace Edge.Data.Pipeline
{
	public struct DateTimeRange
	{
		public DateTimeSpecification Start;
		public DateTimeSpecification End;

		/*
		public DateTimeRange(DateTimeSpecification singleUnit)
		{
			Start = singleUnit; Start.Boundary = DateTimeSpecificationBounds.Lower;
			End = singleUnit; End.Boundary = DateTimeSpecificationBounds.Upper;
		}
		*/

		public static DateTimeRange Parse(string json)
		{
			//reformat the json in order to be able to Deserialize
			if (json.Contains("\""))			
				json=json.Replace("\"", string.Empty);			
			if (json.Contains("\\"))
				json=json.Replace("\\","'");

			JObject jObjecttimeRange = JObject.Parse(json);
			DateTimeRange dateTimeRange=new DateTimeRange();
			JsonSerializerSettings settings=new JsonSerializerSettings();


			dateTimeRange.Start.BaseDateTime = (DateTime)JsonConvert.DeserializeObject<DateTime>(jObjecttimeRange["start"].ToString());
			dateTimeRange.End.BaseDateTime = (DateTime)JsonConvert.DeserializeObject<DateTime>(jObjecttimeRange["end"].ToString());
			return dateTimeRange;
		}
		/// <summary>
		/// Return Start End time as string isonDateTimejson
		/// </summary>
		/// <returns>DateTime as string json</returns>
		// {start: '2009-01-01 23:00:00.00', end: 'iso date'}
		public override string ToString()
		{
			dynamic timeRange = new ExpandoObject();
			timeRange.start=JsonConvert.SerializeObject(Start.ToDateTime(), new IsoDateTimeConverter());
			timeRange.end= JsonConvert.SerializeObject(End.ToDateTime(), new IsoDateTimeConverter());


			 return JsonConvert.SerializeObject(timeRange);
		}

		/// <summary>
		/// Converts the transformations of the range into absolute base date/time values.
		/// </summary>
		/// <returns></returns>
		public DateTimeRange ToAbsolute()
		{
			return new DateTimeRange()
			{
				Start = new DateTimeSpecification()
				{
					BaseDateTime = this.Start.ToDateTime(),
					Boundary = DateTimeSpecificationBounds.Lower
				},
				End = new DateTimeSpecification()
				{
					BaseDateTime = this.End.ToDateTime(),
					Boundary = DateTimeSpecificationBounds.Upper
				}
			};
		}

		#region Default values
		//----------------------

		/// <summary>
		/// {start: {d:-1, h:0}, end: {d:-1, h:'*'}},
		/// {start: '2009-01-01 00:00:00.00', end: '2009-01-01 23:59:59.99999'}
		/// </summary>
		public readonly static DateTimeRange AllOfYesterday = new DateTimeRange()
		{
			Start = new DateTimeSpecification()
			{
				Boundary = DateTimeSpecificationBounds.Lower,
				Day = new DateTimeTransformation()
				{
					Type = DateTimeTransformationType.Relative,
					Value = -1
				},
				Hour = new DateTimeTransformation()
				{
					Type = DateTimeTransformationType.Exact,
					Value = 0
				}
			},
			End = new DateTimeSpecification()
			{
				Boundary = DateTimeSpecificationBounds.Upper,
				Day = new DateTimeTransformation()
				{
					Type = DateTimeTransformationType.Relative,
					Value = -1
				},
				Hour = new DateTimeTransformation()
				{
					Type = DateTimeTransformationType.Max,
				}
			}
		};


		//----------------------
		#endregion
	}

	public enum DateTimeSpecificationBounds
	{
		Lower,
		Upper
	}

	public struct DateTimeSpecification
	{
		public DateTimeSpecificationBounds Boundary;
		public DateTime BaseDateTime;
		public TimeSpan BaseTime;
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
			DateTime result = this.BaseDateTime != DateTime.MinValue ? this.BaseDateTime : DateTime.Now;

			//...........................
			// Year

			result = result.Transform(
				Year,
				this.Boundary,
				(d, v) => d.AddYears(v),
				(d, v) => new DateTime(v, 1, 1),
				(d) => { throw new InvalidOperationException("Year cannot be set to max (*).");  }
			);

			//...........................
			// Month

			result = result.Transform(
				Month,
				this.Boundary,
				(d, v) => d.AddMonths(v),
				(d, v) => new DateTime(d.Year, v, 1),
				(d) => new DateTime(d.Year, 1, 1).AddYears(1).AddMonths(-1)
			);

			//...........................
			// Week
			if (!Week.IsEmpty)
			{
				throw new NotImplementedException("Week transformation not yet implemented.");
				/*
				//limit |= Week.Limit;
				date = Week.Type == DateTimeTransformationType.Exact ?
					(Limit ?
						new DateTime(date.Year, Month.Value, 1).SetWeekOfMonth(Week.Value).AddWeekOfMonth(1).AddTicks(-1) :
						new DateTime(date.Year, Month.Value, 1).SetWeekOfMonth(Week.Value)
					) :
					date.AddDays(Week.Value * 7);
				*/
			}

			//...........................
			// Day

			if (!Week.IsEmpty)
			{
				// TODO: treat days as 1..7 (days of week)
				throw new NotImplementedException("Week transformation not yet implemented.");
			}
			else
			{
				// treat days as day of month
				result = result.Transform(
					Day,
					this.Boundary,
					(d, v) => d.AddDays(v),
					(d, v) => new DateTime(d.Year, d.Month, v),
					(d) => new DateTime(d.Year, d.Month, 1).AddMonths(1).AddDays(-1)
				);
			}

			//...........................
			// Time
			if (BaseTime != TimeSpan.Zero)
			{
				result = new DateTime(result.Year, result.Month, result.Day).Add(BaseTime);
			}

			//...........................
			// Hour
			result = result.Transform(
					Hour,
					this.Boundary,
					(d, v) => d.AddHours(v),
					(d, v) => new DateTime(d.Year, d.Month, d.Day, v, 0, 0),
					(d) => new DateTime(d.Year, d.Month, d.Day, 0, 0, 0).AddDays(1).AddHours(-1)
				);

			//...........................
			// Minute
			result = result.Transform(
					Minute,
					this.Boundary,
					(d, v) => d.AddMinutes(v),
					(d, v) => new DateTime(d.Year, d.Month, d.Day, d.Hour, v, 0),
					(d) => new DateTime(d.Year, d.Month, d.Day, d.Hour, 0, 0).AddHours(1).AddMinutes(-1)
				);

			//...........................
			// Second
			result = result.Transform(
					Second,
					this.Boundary,
					(d, v) => d.AddSeconds(v),
					(d, v) => new DateTime(d.Year, d.Month, d.Day, d.Hour, d.Minute, v),
					(d) => new DateTime(d.Year, d.Month, d.Day, d.Hour, d.Minute, 0).AddMinutes(1).AddSeconds(-1)
				);

			return result;
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
		public static readonly DateTimeTransformation Empty = new DateTimeTransformation();

		public bool IsEmpty
		{
			get { return this.Type == DateTimeTransformationType.None; }
		}

		public override int GetHashCode()
		{
			unchecked
			{
				return Type.GetHashCode() ^ Value.GetHashCode();
			}
		}

		public override bool Equals(object obj)
		{
			if (!(obj is DateTimeTransformation))
				return false;

			var tf = (DateTimeTransformation)obj;
			return tf.Type == this.Type && tf.Value == this.Value;
		}

		public override string ToString()
		{
			string val;

			if (this.Type == DateTimeTransformationType.Max)
			{
				val = "*";
			}
			else if (this.Type == DateTimeTransformationType.Relative && this.Value != 0)
			{
				val = (Value > 0 ? "+=" : "-=") + this.Value.ToString();
			}
			else
			{
				val = Math.Abs(this.Value).ToString();
			}

			return val;
		}

		public static DateTimeRange Parse(string json)
		{
			throw new NotImplementedException();
		}
	}

	public enum DateTimeTransformationType
	{
		None,
		Relative,
		Exact,
		Max
	}

	public static class DateTimeExtensions
	{
		internal static DateTime Transform(this DateTime dateTime, DateTimeTransformation transform, DateTimeSpecificationBounds boundary, Func<DateTime, int, DateTime> relative, Func<DateTime, int, DateTime> exact, Func<DateTime, DateTime> max)
		{
			if (transform.IsEmpty)
				return dateTime;

			DateTime result = dateTime;
			if (transform.Type == DateTimeTransformationType.Relative)
			{
				result = relative(result, transform.Value);
			}
			else
			{
				if (transform.Type == DateTimeTransformationType.Exact)
					result = exact(result, transform.Value);
				else if (transform.Type == DateTimeTransformationType.Max)
					result = max(result);

				if (boundary == DateTimeSpecificationBounds.Upper)
					result = relative(result,1).AddTicks(-1);
			}

			return result;
		}


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
