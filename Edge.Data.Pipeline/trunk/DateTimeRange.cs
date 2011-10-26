using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Dynamic;
using Newtonsoft.Json.Linq;
using System.IO;
using System.ComponentModel;

namespace Edge.Data.Pipeline
{
	[JsonObject(MemberSerialization.OptIn)]
	public struct DateTimeRange
	{
		[JsonProperty(PropertyName = "start")]
		DateTimeSpecification _start;
		[JsonProperty(PropertyName = "end")]
		DateTimeSpecification _end;

		public DateTimeSpecification Start
		{
			get { return _start; }
			set { _start = value; _start.Alignment = DateTimeSpecificationAlignment.Start; }
		}
		public DateTimeSpecification End
		{
			get { return _end; }
			set { _end = value; _end.Alignment = DateTimeSpecificationAlignment.End; }
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
					Alignment = DateTimeSpecificationAlignment.Start
				},
				End = new DateTimeSpecification()
				{
					BaseDateTime = this.End.ToDateTime(),
					Alignment = DateTimeSpecificationAlignment.End
				}
			};
		}

		#region Serialization
		//----------------------

		public static DateTimeRange Parse(string json)
		{
			
			var serializer = new JsonSerializer()
			{
				DefaultValueHandling = DefaultValueHandling.Ignore,
				NullValueHandling = NullValueHandling.Ignore
			};

			using (var reader = new JsonTextReader(new StringReader(json)))
			{
				DateTimeRange range = serializer.Deserialize<DateTimeRange>(reader);
				range._start.Alignment = DateTimeSpecificationAlignment.Start;
				range._end.Alignment = DateTimeSpecificationAlignment.End;
				return range;
			}
		}

		public override string ToString()
		{
			return ToString(false);
		}

		public string ToString(bool indented)
		{
			return JsonHelper.Serialize(this, indented);
		}

		//----------------------
		#endregion

		#region Default values
		//----------------------

		/// <summary>
		/// {start: {d:-1, h:0}, end: {d:-1, h:'*'}},
		/// {start: '2009-01-01T00:00:00.00000Z', end: '2009-01-01T23:59:59.99999Z'}
		/// </summary>
		public readonly static DateTimeRange AllOfYesterday = new DateTimeRange()
		{
			Start = new DateTimeSpecification()
			{
				Alignment = DateTimeSpecificationAlignment.Start,
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
				Alignment = DateTimeSpecificationAlignment.End,
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

	public enum DateTimeSpecificationAlignment
	{
		Start,
		End
	}

	[JsonObject(MemberSerialization.OptIn)]
	public struct DateTimeSpecification
	{
		#region Fields
		/*=========================*/

		[JsonProperty(PropertyName="align"), JsonConverter(typeof(StringEnumConverter))]
		public DateTimeSpecificationAlignment Alignment;

		[JsonProperty(PropertyName = "date"), JsonConverter(typeof(IsoDateTimeConverter)), StructDefaultValue(typeof(DateTime))]
		public DateTime BaseDateTime; // date

		[JsonProperty(PropertyName = "time"), StructDefaultValue(typeof(TimeSpan))]
		public TimeSpan BaseTime; // time

		[JsonIgnore]
		public DayOfWeek FirstDayOfWeek;

		[JsonProperty(PropertyName = "y"), StructDefaultValue(typeof(DateTimeTransformation))]
		public DateTimeTransformation Year;

		[JsonProperty(PropertyName = "m"), StructDefaultValue(typeof(DateTimeTransformation))]
		public DateTimeTransformation Month;

		[JsonProperty(PropertyName = "w"), StructDefaultValue(typeof(DateTimeTransformation))]
		public DateTimeTransformation Week;

		[JsonProperty(PropertyName = "d"), StructDefaultValue(typeof(DateTimeTransformation))]
		public DateTimeTransformation Day;

		[JsonProperty(PropertyName = "h"), StructDefaultValue(typeof(DateTimeTransformation))]
		public DateTimeTransformation Hour;

		[JsonProperty(PropertyName = "mm"), StructDefaultValue(typeof(DateTimeTransformation))]
		public DateTimeTransformation Minute;

		[JsonProperty(PropertyName = "s"), StructDefaultValue(typeof(DateTimeTransformation))]
		public DateTimeTransformation Second;

		/*=========================*/
		#endregion

		public DateTime ToDateTime()
		{
			DateTime result = this.BaseDateTime != DateTime.MinValue ? this.BaseDateTime : DateTime.Now;

			//...........................
			// Year

			result = result.Transform(
				Year,
				this.Alignment,
				(d, v) => d.AddYears(v),
				(d, v) => new DateTime(v, 1, 1),
				(d) => { throw new InvalidOperationException("Year cannot be set to max (*).");  }
			);

			//...........................
			// Month

			result = result.Transform(
				Month,
				this.Alignment,
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
					this.Alignment,
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
					this.Alignment,
					(d, v) => d.AddHours(v),
					(d, v) => new DateTime(d.Year, d.Month, d.Day, v, 0, 0),
					(d) => new DateTime(d.Year, d.Month, d.Day, 0, 0, 0).AddDays(1).AddHours(-1)
				);

			//...........................
			// Minute
			result = result.Transform(
					Minute,
					this.Alignment,
					(d, v) => d.AddMinutes(v),
					(d, v) => new DateTime(d.Year, d.Month, d.Day, d.Hour, v, 0),
					(d) => new DateTime(d.Year, d.Month, d.Day, d.Hour, 0, 0).AddHours(1).AddMinutes(-1)
				);

			//...........................
			// Second
			result = result.Transform(
					Second,
					this.Alignment,
					(d, v) => d.AddSeconds(v),
					(d, v) => new DateTime(d.Year, d.Month, d.Day, d.Hour, d.Minute, v),
					(d) => new DateTime(d.Year, d.Month, d.Day, d.Hour, d.Minute, 0).AddMinutes(1).AddSeconds(-1)
				);

			return result;
		}

		public static DateTimeSpecification Parse(string json)
		{
			var serializer = new JsonSerializer()
			{
				DefaultValueHandling = DefaultValueHandling.Ignore,
				NullValueHandling = NullValueHandling.Ignore
			};

			using (var reader = new JsonTextReader(new StringReader(json)))
			{
				DateTimeSpecification spec = serializer.Deserialize<DateTimeSpecification>(reader);
				return spec;
			}
		}

		public override string ToString()
		{
			return ToString(false);
		}

		public string ToString(bool indented)
		{
			return JsonHelper.Serialize(this, indented);
		}

	}

	public enum DateTimeLimit
	{
		Lower,
		Upper
	}

	[JsonConverter(typeof(DateTimeTransformation.Converter))]
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
			if (obj is string)
				return this.ToString() == obj.ToString();
			else
				return base.Equals(obj);
		}

		//public override bool Equals(object obj)
		//{
		//    if (!(obj is DateTimeTransformation))
		//        return false;

		//    var tf = (DateTimeTransformation)obj;
		//    return tf.Type == this.Type && tf.Value == this.Value;
		//}

		public override string ToString()
		{
			string val;

			if (this.Type == DateTimeTransformationType.Max)
			{
				val = "*";
			}
			else if (this.Type == DateTimeTransformationType.Relative)
			{
				val = (Value == 0 ? "" : Value > 0 ? "+=" : "-=") + Math.Abs(this.Value).ToString();
			}
			else if (this.Type == DateTimeTransformationType.Exact)
			{
				val = Math.Abs(this.Value).ToString();
			}
			else
			{
				val = string.Empty;
			}

			return val;
		}

		public static DateTimeTransformation Parse(string source)
		{
			using (var reader = new JsonTextReader(new StringReader(source)))
			{
				return (DateTimeTransformation) new Converter().ReadJson(reader, typeof(DateTimeTransformation), null, null);
			}
		}

		class Converter : JsonConverter
		{
			public override bool CanConvert(Type objectType)
			{
				return objectType == typeof(DateTimeTransformation);
			}

			public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
			{
				DateTimeTransformation trans = new DateTimeTransformation();
				if (reader.TokenType == JsonToken.Integer)
				{
					int val = Convert.ToInt32(reader.Value);
					trans.Type = val < 0 ? DateTimeTransformationType.Relative : DateTimeTransformationType.Exact;
					trans.Value = val;
				}
				else
				{
					string val = (string)reader.Value;
					if (val == "*")
					{
						trans.Type = DateTimeTransformationType.Max;
					}
					else if (val.StartsWith("-") || val.StartsWith("+"))
					{
						trans.Type = DateTimeTransformationType.Relative;
						string num = val.Replace("-", "").Replace("+", "").Replace("=", "");
						trans.Value = Int32.Parse(num) * (val.StartsWith("-") ? -1 : 1);
					}
					else if (val == string.Empty)
					{
						trans.Type = DateTimeTransformationType.None;
					}
					else
					{
						int numval;
						if (Int32.TryParse(val, out numval))
						{
							// number as string
							trans.Type = DateTimeTransformationType.Exact;
							trans.Value = numval;
						}
						else
							throw new FormatException(String.Format("'{0}' is not a valid value for DateTimeTransformation.", val));
					}

				}

				return trans;
			}

			public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
			{
				var transformation = (DateTimeTransformation) value;
				bool stringify =
					transformation.Type == DateTimeTransformationType.Max ||
					transformation.Type == DateTimeTransformationType.None ||
					(transformation.Type == DateTimeTransformationType.Relative && transformation.Value != 0);
				
				writer.WriteValue(
					stringify ?
						(object) transformation.ToString():
						(object) transformation.Value
					);
			}
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
		internal static DateTime Transform(this DateTime dateTime, DateTimeTransformation transform, DateTimeSpecificationAlignment alignment, Func<DateTime, int, DateTime> relative, Func<DateTime, int, DateTime> exact, Func<DateTime, DateTime> max)
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

				if (alignment == DateTimeSpecificationAlignment.End)
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

	public class StructDefaultValueAttribute : DefaultValueAttribute
	{
		public StructDefaultValueAttribute(Type type) : base(GetStructDefault(type)) { }

		private static object GetStructDefault(Type type)
		{
			return type.IsValueType ? Activator.CreateInstance(type) : null;
		}
	}

	static class JsonHelper
	{
		public static string Serialize(object obj, bool indented)
		{
			var serializer = new JsonSerializer()
			{
				DefaultValueHandling = DefaultValueHandling.Ignore,
				NullValueHandling = NullValueHandling.Ignore
			};

			var stringWriter = new StringWriter();
			using (var writer = new JsonTextWriter(stringWriter)
			{
				QuoteName = false,
				QuoteChar = '\''
			})
			{
				if (indented)
					writer.Formatting = Formatting.Indented;

				serializer.Serialize(writer, obj);
				writer.Close();
				var json = stringWriter.ToString();
				return json;
			}
		}
	}

}
