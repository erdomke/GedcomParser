using System;

namespace GedcomParser
{
    public class DatePeriod : IDateValue, IMutableDateValue
    {
        private string _phrase;

        public DateValue Start { get; }
        public DateValue End { get; }

        public DatePeriod(DateValue start, DateValue end)
        {
            Start = start;
            End = end;
        }

        public override string ToString()
        {
            if (_phrase != null)
                return _phrase;
            return ToString("", null);
        }

        public string ToString(string format, IFormatProvider formatProvider)
        {
            var result = "";
            if (Start != null)
                result = "FROM " + Start.ToString(format, formatProvider);
            if (End != null)
            {
                if (result.Length > 0)
                    result += " ";
                result += "TO " + End.ToString(format, formatProvider);
            }
            return result;
        }

        void IMutableDateValue.SetPhrase(string value)
        {
            _phrase = value;
        }

        void IMutableDateValue.SetTime(TimeSpan timeSpan, bool isUtc)
        {
            (Start as IMutableDateValue)?.SetTime(timeSpan, isUtc);
            (End as IMutableDateValue)?.SetTime(timeSpan, isUtc);
        }
    }
}
