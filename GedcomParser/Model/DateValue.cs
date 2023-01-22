using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace GedcomParser
{
    public class DateValue : IDateValue
    {
        /*
         * 16-bits = year (signed short)
         * 4-bits = month
         * 6-bits = day
         * 5-bits = hour
         * 6-bits = minute
         * 6-bits = second
         * 10-bits = millisecond
         * 2-bits = DateTimeKind
         * 3-bits = Calendar
         * 2-bits = Certainty
         */
        private ulong _data;
        private string _phrase;

        public DateCertainty Certainty
        {
            get => (DateCertainty)(_data & 0b11);
            private set => _data = (_data & ~0b11UL) | ((ulong)value & 0b11UL);
        }

        public DateCalendar Calendar
        {
            get => (DateCalendar)((_data >> 2) & 0b111);
            private set => _data = (_data & ~0b11100UL) | (((ulong)value & 0b111UL) << 2);
        }

        public DateTimeKind Kind
        {
            get => (DateTimeKind)((_data >> 5) & 0b11);
            private set => _data = (_data & ~0b1100000UL) | (((ulong)value & 0b11UL) << 5);
        }

        public int? Year
        {
            get
            {
                var stored = (short)((_data >> 44) & 0xFFFF);
                if (stored == short.MinValue)
                    return null;
                return stored;
            }
            set
            {
                var stored = (ulong)(value.HasValue ? (short)value.Value : short.MinValue) & 0xFFFF;
                _data = (_data & ~(0xFFFFUL << 44))
                    | stored << 44;
            }
        }

        public int? Month
        {
            get
            {
                var stored = (int)((_data >> 40) & 0b1111);
                if (stored == 0)
                    return null;
                return stored;
            }
            set
            {
                var stored = (ulong)(value.HasValue ? value.Value : 0) & 0b1111;
                _data = (_data & ~(0b1111UL << 40))
                    | stored << 40;
            }
        }

        public int? Day
        {
            get
            {
                var stored = (int)((_data >> 34) & 0b111111);
                if (stored == 0)
                    return null;
                return stored;
            }
            set
            {
                var stored = (ulong)(value.HasValue ? value.Value : 0) & 0b111111;
                _data = (_data & ~(0b111111UL << 34))
                    | stored << 34;
            }
        }

        public int? Hour
        {
            get
            {
                var stored = (int)((_data >> 29) & 0b11111);
                if (stored == 0)
                    return null;
                return stored - 1;
            }
            set
            {
                var stored = (ulong)(value.HasValue ? value.Value + 1 : 0) & 0b11111;
                _data = (_data & ~(0b11111UL << 29))
                    | stored << 29;
            }
        }

        public int? Minute
        {
            get
            {
                var stored = (int)((_data >> 23) & 0b111111);
                if (stored == 0)
                    return null;
                return stored - 1;
            }
            set
            {
                var stored = (ulong)(value.HasValue ? value.Value + 1 : 0) & 0b111111;
                _data = (_data & ~(0b111111UL << 23))
                    | stored << 23;
            }
        }

        public int? Second
        {
            get
            {
                var stored = (int)((_data >> 17) & 0b111111);
                if (stored == 0)
                    return null;
                return stored - 1;
            }
            set
            {
                var stored = (ulong)(value.HasValue ? value.Value + 1 : 0) & 0b111111;
                _data = (_data & ~(0b111111UL << 17))
                    | stored << 17;
            }
        }

        public int? Millisecond
        {
            get
            {
                var stored = (int)((_data >> 7) & 0b1111111111);
                if (stored == 0)
                    return null;
                return stored - 1;
            }
            private set
            {
                var stored = (ulong)(value.HasValue ? value.Value + 1 : 0) & 0b1111111111;
                _data = (_data & ~(0b1111111111UL << 7))
                    | stored << 7;
            }
        }

        public DateValue()
        {
            Year = null;
        }

        public bool TryGetCalendar(out Calendar calendar)
        {
            switch (Calendar)
            {
                case DateCalendar.Gregorian:
                    calendar = new GregorianCalendar();
                    return true;
                case DateCalendar.Julian:
                    calendar = new JulianCalendar();
                    return true;
                case DateCalendar.Hebrew:
                    calendar = new HebrewCalendar();
                    return true;
            }
            calendar = null;
            return false;
        }

        public bool TryGetDateTime(out DateTime value)
        {
            if (TryGetCalendar(out var calendar)
                && Day > 0
                && Month > 0
                && Year > 0)
            {
                if (calendar is GregorianCalendar)
                    value = new DateTime(Year.Value, Month.Value, Day.Value, Hour ?? 0, Minute ?? 0, Second ?? 0, Millisecond ?? 0, Kind);
                else
                    value = new DateTime(Year.Value, Month.Value, Day.Value, Hour ?? 0, Minute ?? 0, Second ?? 0, Millisecond ?? 0, calendar, Kind);
                return true;
            }
            else
            {
                value = default;
                return false;
            }
        }

        public override string ToString()
        {
            if (_phrase == null)
            {
                var result = "";
                if (Certainty == DateCertainty.About)
                    result = "ABT ";
                else if (Certainty == DateCertainty.Calculated)
                    result = "CAL ";
                else if (Certainty == DateCertainty.Estimated)
                    result = "EST ";
                if (Calendar != DateCalendar.Gregorian)
                    result += Calendar.ToString().ToUpperInvariant() + " ";
                if (Day.HasValue)
                    result += Day.Value + " ";
                if (Month.HasValue)
                {
                    if (Calendar == DateCalendar.French_R)
                        result += _frenchRMonths[Month.Value - 1] + " ";
                    else if (Calendar == DateCalendar.Hebrew)
                        result += _hebrewMonths[Month.Value - 1] + " ";
                    else
                        result += _gregorianMonths[Month.Value - 1] + " ";
                }
                if (Year.HasValue)
                {
                    if (Year > 0)
                        result += Year.Value;
                    else
                        result += (Year - 1) * -1 + " BCE";
                }
                return result;
            }
            else
            {
                return _phrase;
            }
        }

        public string ToString(string format)
        {
            return ToString(format, CultureInfo.CurrentCulture);
        }

        public string ToString(string format, IFormatProvider formatProvider)
        {
            if (string.IsNullOrEmpty(format) || format == "G")
            {
                return ToString();
            }
            else if (format != "s" && format != "u" && TryGetDateTime(out var dateTime))
            {
                return dateTime.ToString(format, formatProvider);
            }
            else
            {
                if (format == "s" || format == "u")
                {
                    var parts = new[] { "yyyy", "'-'MM", "'-'dd", (format == "s" ? "'T'" : " ") + "HH", "':'mm", "':'ss" };
                    var count = 1;
                    if (Second.HasValue)
                        count = 6;
                    else if (Minute.HasValue)
                        count = 5;
                    else if (Hour.HasValue)
                        count = 4;
                    else if (Day.HasValue)
                        count = 3;
                    else if (Month.HasValue)
                        count = 2;
                    format = string.Join("", parts, 0, count) + (format == "u" ? "'Z'" : "");
                }

                var builder = new StringBuilder();
                var index = 0;
                var dateFormat = DateTimeFormat(formatProvider);
                while (TryConsumeSpecifier(format, formatProvider, ref index, out var specifier, out var constant))
                {
                    switch (specifier ?? "")
                    {
                        case "d":
                            builder.Append(Day.HasValue ? Day.Value.ToString() : "X");
                            break;
                        case "dd":
                            builder.Append(Day.HasValue ? Day.Value.ToString("D2") : "XX");
                            break;
                        case "g":
                        case "gg":
                            if (Year <= 0)
                                builder.Append("B.C.");
                            else
                                builder.Append(dateFormat.GetEraName(0));
                            break;
                        case "M":
                            builder.Append(Month.HasValue ? Month.Value.ToString() : "X");
                            break;
                        case "MM":
                            builder.Append(Month.HasValue ? Month.Value.ToString("D2") : "XX");
                            break;
                        case "MMM":
                            builder.Append(Month.HasValue ? dateFormat.GetAbbreviatedMonthName(Month.Value) : "XXX");
                            break;
                        case "MMMM":
                            builder.Append(Month.HasValue ? dateFormat.GetMonthName(Month.Value) : "XXXX");
                            break;
                        case "y":
                            builder.Append(Year.HasValue ? (Year.Value % 100).ToString() : "X");
                            break;
                        case "yy":
                            builder.Append(Year.HasValue ? (Year.Value % 100).ToString("D2") : "XX");
                            break;
                        case "yyy":
                            builder.Append(Year.HasValue ? Year.Value.ToString("D3") : "XXX");
                            break;
                        case "yyyy":
                            builder.Append(Year.HasValue ? Year.Value.ToString("D4") : "XXXX");
                            break;
                        case "yyyyy":
                            builder.Append(Year.HasValue ? Year.Value.ToString("D5") : "XXXXX");
                            break;
                        case "":
                            builder.Append(constant);
                            break;
                        default:
                            throw new InvalidOperationException($"Invalid format string {format}");
                    }
                }
                return builder.ToString();
            }
        }

        private static bool TryConsumeSpecifier(string format, IFormatProvider formatProvider, ref int index, out string specifier, out string constant)
        {
            specifier = null;
            constant = null;
            if (index >= format.Length)
                return false;

            if (format[index] == '%')
            {
                index++;
                if (index >= format.Length)
                    throw new InvalidOperationException($"Invalid date/time format: {format}"); ;
            }

            switch (format[index])
            {
                case 'd':
                case 'f':
                case 'F':
                case 'g':
                case 'h':
                case 'H':
                case 'K':
                case 'm':
                case 'M':
                case 's':
                case 't':
                case 'y':
                case 'z':
                    var next = index + 1;
                    while (next < format.Length && format[next] == format[index])
                        next++;
                    specifier = format.Substring(index, next - index);
                    index = next;
                    break;
                case ':':
                    constant = DateTimeFormat(formatProvider).TimeSeparator;
                    index++;
                    break;
                case '/':
                    constant = DateTimeFormat(formatProvider).DateSeparator;
                    index++;
                    break;
                case '"':
                case '\'':
                    var end = format.IndexOf(format[index], index + 1);
                    if (end < 0)
                        throw new InvalidOperationException($"Invalid date/time format: {format}");
                    constant = format.Substring(index + 1, end - index - 1);
                    index = end + 1;
                    break;
                default:
                    constant = format.Substring(index, 1);
                    index++;
                    break;
            }
            return true;
        }

        private static DateTimeFormatInfo DateTimeFormat(IFormatProvider formatProvider)
        {
            return formatProvider?.GetFormat(typeof(DateTimeFormatInfo)) as DateTimeFormatInfo
                ?? CultureInfo.InvariantCulture.DateTimeFormat;
        }

        public static DateValue Parse(string value)
        {
            if (TryParse(value, out var result))
                return result;
            throw new FormatException($"{value} is not a valid date.");
        }

        public static DateValue ParseGedcom(string value)
        {
            if (TryParseGedcom(value, out var result))
                return result;
            throw new FormatException($"{value} is not a valid GEDCOM date.");
        }

        public static bool TryParse(string value, out DateValue date)
        {
            return TryParse(value, CultureInfo.CurrentCulture, out date);
        }

        public static bool TryParse(string value, IFormatProvider provider, out DateValue date)
        {
            if (TryParseGedcom(value, out date))
                return true;

            var match = Regex.Match(value, @"^(?<year>\d{4}|XXXX)(-(?<month>\d{2}|XX)(-(?<day>\d{2}|XX)([ T](?<hour>\d{2}|XX)(:(?<minute>\d{2}|XX)(:(?<second>\d{2}|XX))?)?)?)?)?(?<utc>Z)?$");
            if (match.Success)
            {
                date = new DateValue()
                {
                    Calendar = DateCalendar.Gregorian,
                    Certainty = DateCertainty.Known,
                    Kind = match.Groups["utc"].Success ? DateTimeKind.Utc : DateTimeKind.Local,
                };
                if (match.Groups["year"].Success && int.TryParse(match.Groups["year"].Value, out var year))
                    date.Year = year;
                if (match.Groups["month"].Success && int.TryParse(match.Groups["month"].Value, out var month))
                    date.Month = month;
                if (match.Groups["day"].Success && int.TryParse(match.Groups["day"].Value, out var day))
                    date.Day = day;
                if (match.Groups["hour"].Success && int.TryParse(match.Groups["hour"].Value, out var hour))
                    date.Hour = hour;
                if (match.Groups["minute"].Success && int.TryParse(match.Groups["minute"].Value, out var minute))
                    date.Minute = minute;
                if (match.Groups["second"].Success && int.TryParse(match.Groups["second"].Value, out var second))
                    date.Second = second;
                return true;
            }

            var patterns = new HashSet<string>(DateTimeFormat(provider)
                .GetAllDateTimePatterns()
                .Where(p => p.IndexOf("yyy") >= 0));
            patterns.Add("d MMMM yyyy");
            foreach (var pattern in patterns)
            {
                if (DateTime.TryParseExact(value, pattern, provider, DateTimeStyles.None, out var dateTime))
                {
                    date = new DateValue()
                    {
                        Calendar = DateCalendar.Gregorian,
                        Certainty = DateCertainty.Known,
                        Kind = dateTime.Kind,
                    };
                    var index = 0;
                    while (TryConsumeSpecifier(pattern, provider, ref index, out var specifier, out var constant))
                    {
                        if (specifier != null)
                        {
                            if (specifier[0] == 'y')
                                date.Year = dateTime.Year;
                            else if (specifier[0] == 'M')
                                date.Month = dateTime.Month;
                            else if (specifier[0] == 'd')
                                date.Day = dateTime.Day;
                            else if (specifier[0] == 'H' || specifier[0] == 'h')
                                date.Hour = dateTime.Hour;
                            else if (specifier[0] == 'm')
                                date.Minute = dateTime.Minute;
                            else if (specifier[0] == 's')
                                date.Second = dateTime.Second;
                            else if (specifier[0] == 'f')
                                date.Millisecond = dateTime.Millisecond;
                        }
                    }
                    return true;
                }
            }
            return false;
        }

        public static bool TryParseGedcom(string value, out DateValue date)
        {
            var parts = value.Split(' ');
            var idx = 0;
            date = new DateValue()
            {
                Certainty = ConsumeApproximate(parts, ref idx),
                Calendar = ConsumeCalendar(parts, ref idx),
                Day = ConsumeDay(parts, ref idx)
            };
            date.Month = ConsumeMonth(parts, date.Calendar, ref idx);
            date.Year = ConsumeYear(parts, ref idx);
            if (ConsumeEpoch(parts, ref idx) && date.Year.HasValue)
                date.Year = date.Year * -1 + 1;

            return idx >= parts.Length && date.Year.HasValue;
        }

        private static DateCertainty ConsumeApproximate(string[] parts, ref int idx)
        {
            if (idx < parts.Length)
            {
                switch (parts[idx].ToUpperInvariant())
                {
                    case "ABT":
                        idx++;
                        return DateCertainty.About;
                    case "CAL":
                        idx++;
                        return DateCertainty.Calculated;
                    case "EST":
                        idx++;
                        return DateCertainty.Estimated;
                }
            }
            return DateCertainty.Known;
        }

        private static DateCalendar ConsumeCalendar(string[] parts, ref int idx)
        {
            if (idx < parts.Length)
            {
                if (int.TryParse(parts[idx], out var _))
                    return DateCalendar.Gregorian;
                else if (Enum.TryParse<DateCalendar>(parts[idx], true, out var calendar)
                    && calendar != DateCalendar.Custom)
                {
                    idx++;
                    return calendar;
                }
                else if (parts[idx].StartsWith("_"))
                {
                    idx++;
                    return DateCalendar.Custom;
                }
            }
            return DateCalendar.Gregorian;
        }

        private static int? ConsumeDay(string[] parts, ref int idx)
        {
            if (idx + 2 < parts.Length
                && int.TryParse(parts[idx], out var day)
                && day >= 1 && day <= 36)
            {
                idx++;
                return day;
            }
            else
            {
                return null;
            }
        }

        private static int? ConsumeMonth(string[] parts, DateCalendar calendar, ref int idx)
        {
            if (idx + 1 < parts.Length)
            {
                var month = -1;
                switch (calendar)
                {
                    case DateCalendar.Gregorian:
                    case DateCalendar.Julian:
                        month = Array.IndexOf(_gregorianMonths, parts[idx].ToUpperInvariant());
                        break;
                    case DateCalendar.French_R:
                        month = Array.IndexOf(_frenchRMonths, parts[idx].ToUpperInvariant());
                        break;
                    case DateCalendar.Hebrew:
                        month = Array.IndexOf(_hebrewMonths, parts[idx].ToUpperInvariant());
                        break;
                }

                if (month >= 0)
                {
                    idx++;
                    return month + 1;
                }
            }
            return null;
        }

        private static int? ConsumeYear(string[] parts, ref int idx)
        {
            if (idx < parts.Length && int.TryParse(parts[idx], out var year))
            {
                idx++;
                return year;
            }
            else
            {
                return null;
            }
        }

        private static bool ConsumeEpoch(string[] parts, ref int idx)
        {
            if (idx < parts.Length && parts[idx] == "BCE")
            {
                idx++;
                return true;
            }
            return false;
        }

        private static string[] _gregorianMonths = new[] { "JAN", "FEB", "MAR", "APR", "MAY", "JUN", "JUL", "AUG", "SEP", "OCT", "NOV", "DEC" };
        private static string[] _frenchRMonths = new[] { "VEND", "BRUM", "FRIM", "NIVO", "PLUV", "VENT", "GERM", "FLOR", "PRAI", "MESS", "THER", "FRUC", "COMP" };
        private static string[] _hebrewMonths = new[] { "TSH", "CSH", "KSL", "TVT", "SHV", "ADR", "ADS", "NSN", "IYR", "SVN", "TMZ", "AAV", "ELL" };
    }
}
