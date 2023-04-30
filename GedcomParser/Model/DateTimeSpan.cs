using System;

namespace GedcomParser
{
    public struct DateTimeSpan //: IFormattable
    {
        /// <summary>
        /// The years component
        /// </summary>
        public int Years => FullMonths / 12;
        /// <summary>
        /// Gets the value expressed in whole and fractional years
        /// </summary>
        public double TotalYears => FullMonths / 12.0 + Span.TotalDays / 365.2425;
        /// <summary>
        /// The months component
        /// </summary>
        public int Months => FullMonths % 12;
        /// <summary>
        /// Total number of full / integral months
        /// </summary>
        public int FullMonths { get; }
        /// <summary>
        /// Gets the value expressed in whole and fractional months
        /// </summary>
        public double TotalMonths => FullMonths + Span.TotalDays / 30.436875;

        public int Days => Span.Days;

        public int Hours => Span.Hours;

        public int Minutes => Span.Minutes;

        public int Seconds => Span.Seconds;


        /// <summary>
        /// Gets the days component of the span
        /// </summary>
        public TimeSpan Span { get; }

        public static DateTimeSpan Zero { get; } = new DateTimeSpan();

        public DateTimeSpan(int years, int months = 0, int weeks = 0, int days = 0, TimeSpan time = default)
        {
            FullMonths = years * 12 + months;
            Span = new TimeSpan(weeks * 7 + days, 0, 0, 0) + time;
        }

        public DateTimeSpan(DateTime start, DateTime end)
        {
            var min = start;
            var max = end;
            var negate = end < start;
            if (negate)
            {
                max = start;
                min = end;
            }

            FullMonths = (max.Year - min.Year) * 12 + (max.Month - min.Month);
            min = min.AddMonths(FullMonths);
            if (min > max)
            {
                FullMonths--;
                min = min.AddMonths(-1);
            }
            Span = (max - min) * (negate ? -1 : 1);
            FullMonths = FullMonths * (negate ? -1 : 1);
        }

        private DateTimeSpan(int months, TimeSpan span)
        {
            FullMonths = months;
            Span = span;
        }

        public DateTimeSpan Negate()
        {
            return new DateTimeSpan(FullMonths * -1, Span.Negate());
        }

        //public string ToString(string format, IFormatProvider formatProvider)
        //{
            
        //}

        //public bool TryParse(string value, out DateTimeSpan dateTimeSpan)
        //{
        //    if (value?.Length < 1)
        //    {
        //        dateTimeSpan = default;
        //        return false;
        //    }
        //    else if (int.TryParse(value, out var years))
        //    {
        //        dateTimeSpan = new DateTimeSpan(years);
        //    }
        //    else if (value.StartsWith("P")
        //        || value.StartsWith("-P"))
        //    {
        //        var negate = value.StartsWith("-P");
        //        var idx = negate ? 2 : 1;

        //    }
        //    else
        //    {

        //    }
        //}


    }
}
