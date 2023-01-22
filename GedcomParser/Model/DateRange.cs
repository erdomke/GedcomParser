using System;

namespace GedcomParser
{
    public class DateRange : IDateValue, IMutableDateValue
    {
        private string _phrase;

        public DateValue After { get; }
        public DateValue Before { get; }

        public DateRange(DateValue after, DateValue before)
        {
            After = after;
            Before = before;
        }

        public override string ToString()
        {
            return ToString("", null);
        }

        public string ToString(string format, IFormatProvider formatProvider)
        {
            if (After != null && Before != null)
                return $"BET {After.ToString(format, formatProvider)} AND {Before.ToString(format, formatProvider)}";
            else if (After != null)
                return "AFT " + After.ToString(format, formatProvider);
            else if (Before != null)
                return "BEF " + Before.ToString(format, formatProvider);
            else
                return "";
        }

        void IMutableDateValue.SetPhrase(string value)
        {
            _phrase = value;
        }

        void IMutableDateValue.SetTime(TimeSpan timeSpan, bool isUtc)
        {
            (After as IMutableDateValue)?.SetTime(timeSpan, isUtc);
            (Before as IMutableDateValue)?.SetTime(timeSpan, isUtc);
        }
    }
}
