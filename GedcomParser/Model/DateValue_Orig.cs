//using System;
//using System.Globalization;
//using System.Text;

//namespace GedcomParser
//{
//    public class DateValue_Orig : IDateValue, IMutableDateValue
//    {
//        private string _phrase;
//        /*
//         * 16-bits = year (signed short)
//         * 4-bits = month
//         * 6-bits = day
//         * 5-bits = hour
//         * 6-bits = minute
//         * 6-bits = second
//         * 10-bits = millisecond
//         * 2-bits = DateTimeKind
//         * 2-bits = Certainty
//         */
//        private UInt64 _data;

//        public string Calendar { get; private set; }
//        public DateCertainty Certainty { get; private set; }
//        public int Year { get; private set; }
//        public int Month { get; private set; }
//        public int Day { get; private set; }
//        public TimeSpan? Time { get; private set; }
//        public bool IsUtc { get; private set; }

//        public DateValue_Orig(string value)
//        {
//            var parts = value.Split(' ');
//            var idx = 0;
//            ConsumeApproximate(parts, ref idx);
//            ConsumeCalendar(parts, ref idx);
//            ConsumeDay(parts, ref idx);
//            ConsumeMonth(parts, ref idx);
//            ConsumeYear(parts, ref idx);
//            ConsumeEpoch(parts, ref idx);
//            if (idx < parts.Length || Year < 0)
//            {
//                Calendar = null;
//                Certainty = DateCertainty.Known;
//                IsBce = false;
//                Year = -1;
//                Month = -1;
//                Day = -1;
//                Time = null;
//                _phrase = value;
//            }
//        }

//        public override string ToString()
//        {
//            if (_phrase == null)
//            {
//                var result = "";
//                if (Certainty == DateCertainty.About)
//                    result = "ABT ";
//                else if (Certainty == DateCertainty.Calculated)
//                    result = "CAL ";
//                else if (Certainty == DateCertainty.Estimated)
//                    result = "EST ";
//                if (Calendar != "GREGORIAN")
//                    result += Calendar + " ";
//                if (Day > 0)
//                    result += Day + " ";
//                if (Month > 0)
//                {
//                    if (Calendar == "FRENCH_R")
//                        result += _frenchRMonths[Month - 1] + " ";
//                    else if (Calendar == "HEBREW")
//                        result += _hebrewMonths[Month - 1] + " ";
//                    else
//                        result += _gregorianMonths[Month - 1] + " ";
//                }
//                if (Year >= 0)
//                    result += Year;
//                if (IsBce)
//                    result += " BCE";
//                return result;
//            }
//            else
//            {
//                return _phrase;
//            }
//        }

//        public string ToString(string format)
//        {
//            return ToString(format, CultureInfo.CurrentCulture);
//        }

//        private void ConsumeApproximate(string[] parts, ref int idx)
//        {
//            if (idx < parts.Length)
//            {
//                switch (parts[idx])
//                {
//                    case "ABT":
//                        Certainty = DateCertainty.About;
//                        idx++;
//                        break;
//                    case "CAL":
//                        Certainty = DateCertainty.Calculated;
//                        idx++;
//                        break;
//                    case "EST":
//                        Certainty = DateCertainty.Estimated;
//                        idx++;
//                        break;
//                }
//            }
//        }

//        private void ConsumeCalendar(string[] parts, ref int idx)
//        {
//            Calendar = "GREGORIAN";
//            if (idx < parts.Length)
//            {
//                switch (parts[idx])
//                {
//                    case "GREGORIAN":
//                    case "JULIAN":
//                    case "FRENCH_R":
//                    case "HEBREW":
//                    case "ROMAN":
//                    case "UNKNOWN":
//                        Calendar = parts[idx];
//                        idx++;
//                        break;
//                    default:
//                        if (parts[idx].StartsWith("_"))
//                        {
//                            Calendar = parts[idx];
//                            idx++;
//                        }
//                        break;
//                }
//            }
//        }

//        private void ConsumeDay(string[] parts, ref int idx)
//        {
//            if (idx + 2 < parts.Length 
//                && int.TryParse(parts[idx], out var day)
//                && day >= 1 && day <= 36)
//            {
//                Day = day;
//                idx++;
//            }
//            else
//            {
//                Day = -1;
//            }
//        }

//        private void ConsumeMonth(string[] parts, ref int idx)
//        {
//            Month = -1;
//            if (idx + 1 < parts.Length)
//            {
//                switch (Calendar)
//                {
//                    case "GREGORIAN":
//                    case "JULIAN":
//                        Month = Array.IndexOf(_gregorianMonths, parts[idx]);
//                        break;
//                    case "FRENCH_R":
//                        Month = Array.IndexOf(_frenchRMonths, parts[idx]);
//                        break;
//                    case "HEBREW":
//                        Month = Array.IndexOf(_hebrewMonths, parts[idx]);
//                        break;
//                }

//                if (Month >= 0)
//                {
//                    Month++;
//                    idx++;
//                }
//            }
//        }

//        private void ConsumeYear(string[] parts, ref int idx)
//        {
//            if (idx < parts.Length && int.TryParse(parts[idx], out var year))
//            {
//                Year = year;
//                idx++;
//            }
//            else
//            {
//                Year = -1;
//            }
//        }

//        private void ConsumeEpoch(string[] parts, ref int idx)
//        {
//            if (idx < parts.Length && parts[idx] == "BCE")
//            {
//                IsBce = true;
//                idx++;
//            }
//        }

//        public bool TryGetCalendar(out Calendar calendar)
//        {
//            switch (Calendar)
//            {
//                case "GREGORIAN":
//                    calendar = new GregorianCalendar();
//                    return true;
//                case "JULIAN":
//                    calendar = new JulianCalendar();
//                    return true;
//                case "HEBREW":
//                    calendar = new HebrewCalendar();
//                    return true;
//            }
//            calendar = null;
//            return false;
//        }

//        public bool TryGetDateTime(out DateTime value)
//        {
//            if (TryGetCalendar(out var calendar)
//                && !IsBce
//                && Day > 0 
//                && Month > 0 
//                && Year >= 0)
//            {
//                if (Time.HasValue)
//                {
//                    if (calendar is GregorianCalendar)
//                        value = new DateTime(Year, Month, Day, Time.Value.Hours, Time.Value.Minutes, Time.Value.Seconds, Time.Value.Milliseconds, IsUtc ? DateTimeKind.Utc : DateTimeKind.Local);
//                    else
//                        value = new DateTime(Year, Month, Day, Time.Value.Hours, Time.Value.Minutes, Time.Value.Seconds, Time.Value.Milliseconds, calendar, IsUtc ? DateTimeKind.Utc : DateTimeKind.Local);
//                }
//                else
//                {
//                    if (calendar is GregorianCalendar)
//                        value = new DateTime(Year, Month, Day);
//                    else
//                        value = new DateTime(Year, Month, Day, calendar);
//                }
//                return true;
//            }
//            else
//            {
//                value = default;
//                return false;
//            }
//        }

//        public string ToString(string format, IFormatProvider formatProvider)
//        {
//            if (string.IsNullOrEmpty(format) || format == "G")
//            {
//                return ToString();
//            }
//            else if (TryGetDateTime(out var dateTime))
//            {
//                return dateTime.ToString(format, formatProvider);
//            }
//            else
//            {
//                var builder = new StringBuilder();
//                var index = 0;
//                var dateFormat = DateTimeFormat(formatProvider);
//                while (TryConsumeSpecifier(format, formatProvider, ref index, out var specifier, out var constant))
//                {
//                    switch (specifier ?? "")
//                    {
//                        case "d":
//                            builder.Append(Day < 0 ? "?" : Day.ToString());
//                            break;
//                        case "dd":
//                            builder.Append(Day < 0 ? "??" : Day.ToString("D2"));
//                            break;
//                        case "g":
//                        case "gg":
//                            if (IsBce)
//                                builder.Append("B.C.");
//                            else
//                                builder.Append(dateFormat.GetEraName(0));
//                            break;
//                        case "M":
//                            builder.Append(Month < 0 ? "?" : Month.ToString());
//                            break;
//                        case "MM":
//                            builder.Append(Month < 0 ? "??" : Month.ToString("D2"));
//                            break;
//                        case "MMM":
//                            builder.Append(Month < 0 ? "???" : dateFormat.GetAbbreviatedMonthName(Month));
//                            break;
//                        case "MMMM":
//                            builder.Append(Month < 0 ? "????" : dateFormat.GetMonthName(Month));
//                            break;
//                        case "y":
//                            builder.Append(Year < 0 ? "?" : (Year % 100).ToString());
//                            break;
//                        case "yy":
//                            builder.Append(Year < 0 ? "??" : (Year % 100).ToString("D2"));
//                            break;
//                        case "yyy":
//                            builder.Append(Year < 0 ? "???" : Year.ToString("D3"));
//                            break;
//                        case "yyyy":
//                            builder.Append(Year < 0 ? "????" : Year.ToString("D4"));
//                            break;
//                        case "yyyyy":
//                            builder.Append(Year < 0 ? "?????" : Year.ToString("D5"));
//                            break;
//                        case "":
//                            builder.Append(constant);
//                            break;
//                        default:
//                            throw new InvalidOperationException($"Invalid format string {format}");
//                    }
//                }
//                return builder.ToString();
//            }
//        }

//        private bool TryConsumeSpecifier(string format, IFormatProvider formatProvider, ref int index, out string specifier, out string constant)
//        {
//            specifier = null;
//            constant = null;
//            if (index >= format.Length)
//                return false;

//            if (format[index] == '%')
//            {
//                index++;
//                if (index >= format.Length)
//                    throw new InvalidOperationException($"Invalid date/time format: {format}"); ;
//            }

//            switch (format[index])
//            {
//                case 'd':
//                case 'f':
//                case 'F':
//                case 'g':
//                case 'h':
//                case 'H':
//                case 'K':
//                case 'm':
//                case 'M':
//                case 's':
//                case 't':
//                case 'y':
//                case 'z':
//                    var next = index + 1;
//                    while (next < format.Length && format[next] == format[index])
//                        next++;
//                    specifier = format.Substring(index, next - index);
//                    index = next;
//                    break;
//                case ':':
//                    constant = DateTimeFormat(formatProvider).TimeSeparator;
//                    index++;
//                    break;
//                case '/':
//                    constant = DateTimeFormat(formatProvider).DateSeparator;
//                    index++;
//                    break;
//                case '"':
//                case '\'':
//                    var end = format.IndexOf(format[index], index + 1);
//                    if (end < 0)
//                        throw new InvalidOperationException($"Invalid date/time format: {format}");
//                    constant = format.Substring(index + 1, end - index - 1);
//                    index = end + 1;
//                    break;
//                default:
//                    constant = format.Substring(index, 1);
//                    index++;
//                    break;
//            }
//            return true;
//        }

//        private DateTimeFormatInfo DateTimeFormat(IFormatProvider formatProvider)
//        {
//            return formatProvider?.GetFormat(typeof(DateTimeFormatInfo)) as DateTimeFormatInfo
//                ?? CultureInfo.InvariantCulture.DateTimeFormat;
//        }

//        void IMutableDateValue.SetPhrase(string value)
//        {
//            _phrase = value;
//        }

//        void IMutableDateValue.SetTime(TimeSpan timeSpan, bool isUtc)
//        {
//            Time = timeSpan;
//            IsUtc = isUtc;
//        }

//        private static string[] _gregorianMonths = new[] { "JAN", "FEB", "MAR", "APR", "MAY", "JUN", "JUL", "AUG", "SEP", "OCT", "NOV", "DEC" };
//        private static string[] _frenchRMonths = new[] { "VEND", "BRUM", "FRIM", "NIVO", "PLUV", "VENT", "GERM", "FLOR", "PRAI", "MESS", "THER", "FRUC", "COMP" };
//        private static string[] _hebrewMonths = new[] { "TSH", "CSH", "KSL", "TVT", "SHV", "ADR", "ADS", "NSN", "IYR", "SVN", "TMZ", "AAV", "ELL" };
//    }
//}
