using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.ComponentModel;

namespace Edge.Core.Utilities
{
    public struct DayCode
    {
        #region Members
		/*=========================*/

        private DateTime _dayCode;

		/*=========================*/
        #endregion

        #region Overrides
		/*=========================*/

        public override bool Equals(object obj)
        {
            DateTime d = GenerateDateTime(obj);

            if (d == _dayCode)
                return true;
            else
                return false;
        }

        public override string ToString()
        {
            return ToDayCode(_dayCode).ToString();
        }

		/*=========================*/
        #endregion

        #region Public Methods
		/*=========================*/

        /// <summary>
        /// Generates a DateTime object from the parameter. You should either pass a string
        /// or a date time object to this method.
        /// </summary>
        /// <param name="d">The parameter</param>
        /// <returns>A generated date time</returns>
        public static DateTime GenerateDateTime(object d)
        {
            if (d.GetType() == typeof(DateTime))
                return Convert.ToDateTime(d);

			int year = 0;
			int month = 0;
			int day = 0;
			bool result = false;

            //This is probably a string, try and parse it.
            string date = d.ToString();
            // Fetch Year
            result = int.TryParse(date.Substring(0, 4), out year);

            if (!result)
            {
                throw new Exception(string.Format("Can't convert year from date string: {0}.", date));
            }

            // Fetch Month
            result = int.TryParse(date.Substring(4, 2), out month);

            if (!result)
            {
                throw new Exception(string.Format("Can't convert month from date string: {0}.", date));
            }

            // Fetch Day
            result = int.TryParse(date.Substring(6, 2), out day);
            if (!result)
            {
                throw new Exception(string.Format("Can't convert Day from date string: {0}.", date));
            }

            try
            {
                DateTime ret = new DateTime(year, month, day);
                return ret;
            }
            catch (ArgumentOutOfRangeException ex)
            {
				throw new ArgumentOutOfRangeException(string.Format("Can't convert {0} to date, Exception message: {1}.", date, ex.Message));
            }
            catch (Exception ex)
            {
				throw new ArgumentOutOfRangeException(string.Format("Can't convert {0} to date, Exception message: {1}.", date, ex.Message));
            }
        }

        /// <summary>
        /// Converts a DateTime class to a day code value.
        /// </summary>
        /// <param name="dateToConvert">The DateTime to convert</param>
        /// <returns>The day code</returns>
        public static int ToDayCode(DateTime dateToConvert)
        {
            return dateToConvert.Year * 10000 + dateToConvert.Month * 100 + dateToConvert.Day;
        }

		/*=========================*/
        #endregion

    }
}