using System;
using System.Globalization;

namespace GedcomParser
{
    public struct ExtendedDateRange
    {
        public bool HasValue => Type != DateRangeType.Unknown;
        public DateRangeType Type { get; private set; }
        public ExtendedDateTime Start { get; private set; }
        public ExtendedDateTime End { get; private set; }

        public bool TryGetRange(out DateTime? start, out DateTime? end)
        {
            start = null;
            end = null;
            if (Type == DateRangeType.Unknown)
                return false;
            else if (Type == DateRangeType.Date)
            {
                if (Start.TryGetRange(out var s, out var e))
                {
                    start = s;
                    end = e;
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                if (!Start.HasValue)
                    start = null;
                else if (Start.TryGetRange(out var s, out var e))
                    start = s;
                else
                    return false;

                if (!End.HasValue)
                    end = null;
                else if (End.TryGetRange(out var s, out var e))
                    end = e;
                else
                    return false;

                return true;
            }
        }

        public override string ToString()
        {
            return ToString("", null);
        }

        public string ToString(string format)
        {
            return ToString(format, null);
        }

        public string ToString(string format, IFormatProvider formatProvider)
        {
            if (Type == DateRangeType.Unknown)
                return "?";
            else if (Type == DateRangeType.Date)
            {
                return Start.ToString(format, formatProvider);
            }
            else if (format == "s" || format == "u")
            {
                return (!Start.HasValue ? ".." : Start.ToString(format, formatProvider))
                    + "/"
                    + (!End.HasValue ? ".." : End.ToString(format, formatProvider));
            }
            else
            {
                if (Type == DateRangeType.Period)
                {
                    if (Start.HasValue)
                        return "FROM " + Start.ToString(format, formatProvider)
                            + " TO " + End.ToString(format, formatProvider);
                    else
                        return "TO " + End.ToString(format, formatProvider);
                }
                else
                {
                    if (Start.HasValue && End.HasValue)
                        return "BET " + Start.ToString(format, formatProvider)
                            + " AND " + End.ToString(format, formatProvider);
                    else if (Start.HasValue)
                        return "AFT " + Start.ToString(format, formatProvider);
                    else
                        return "BEF " + End.ToString(format, formatProvider);
                }
            }
        }

        public static ExtendedDateRange Parse(string value)
        {
            if (TryParse(value, out var result))
                return result;
            throw new FormatException($"{value} is not a valid date range.");
        }

        public static bool TryParse(string value, out ExtendedDateRange range)
        {
            return TryParse(value, CultureInfo.CurrentCulture, out range);
        }

        public static bool TryParse(string value, IFormatProvider provider, out ExtendedDateRange range)
        {
            if (string.IsNullOrEmpty(value))
            {
                range = default;
                return false;
            }
            else if (value.StartsWith("FROM ", StringComparison.OrdinalIgnoreCase))
            {
                var idx = value.IndexOf(" TO ", StringComparison.OrdinalIgnoreCase);
                range = new ExtendedDateRange() { Type = DateRangeType.Period };
                if (idx > 0)
                {
                    if (ExtendedDateTime.TryParse(value.Substring(5, idx - 5), provider, out var start))
                        range.Start = start;
                    else
                        return false;
                    if (ExtendedDateTime.TryParse(value.Substring(idx + 4), provider, out var end))
                        range.End = end;
                    else
                        return false;
                }
                else
                {
                    if (ExtendedDateTime.TryParse(value.Substring(5), provider, out var start))
                        range.Start = start;
                    else
                        return false;
                }
            }
            else if (value.StartsWith("TO ", StringComparison.OrdinalIgnoreCase))
            {
                range = new ExtendedDateRange() { Type = DateRangeType.Period };
                if (ExtendedDateTime.TryParse(value.Substring(3), provider, out var end))
                    range.End = end;
                else
                    return false;
            }
            else if (value.StartsWith("BET ", StringComparison.OrdinalIgnoreCase)
                || value.StartsWith("BETWEEN ", StringComparison.OrdinalIgnoreCase))
            {
                var startIdx = value.StartsWith("BET ", StringComparison.OrdinalIgnoreCase) ? 4 : 8;
                range = new ExtendedDateRange() { Type = DateRangeType.Range };
                var idx = value.IndexOf(" AND ");
                if (idx > 0)
                {
                    if (ExtendedDateTime.TryParse(value.Substring(startIdx, idx - startIdx), provider, out var start))
                        range.Start = start;
                    else
                        return false;
                    if (ExtendedDateTime.TryParse(value.Substring(idx + 5), provider, out var end))
                        range.End = end;
                    else
                        return false;
                }
                else
                {
                    return false;
                }
            }
            else if (value.StartsWith("AFT ", StringComparison.OrdinalIgnoreCase)
                || value.StartsWith("AFTER ", StringComparison.OrdinalIgnoreCase))
            {
                var startIdx = value.StartsWith("AFT ", StringComparison.OrdinalIgnoreCase) ? 4 : 6;
                range = new ExtendedDateRange() { Type = DateRangeType.Range };
                if (ExtendedDateTime.TryParse(value.Substring(startIdx), provider, out var start))
                    range.Start = start;
                else
                    return false;
            }
            else if (value.StartsWith("BEF ", StringComparison.OrdinalIgnoreCase)
                || value.StartsWith("BEFORE ", StringComparison.OrdinalIgnoreCase))
            {
                var startIdx = value.StartsWith("BEF ", StringComparison.OrdinalIgnoreCase) ? 4 : 7;
                range = new ExtendedDateRange() { Type = DateRangeType.Range };
                if (ExtendedDateTime.TryParse(value.Substring(startIdx), provider, out var end))
                    range.End = end;
                else
                    return false;
            }
            else
            {
                var parts = value.Split('/');
                if (parts.Length == 2
                    && (parts[0] == "" || parts[0] == ".." || ExtendedDateTime.ExtendedDateTimePattern.IsMatch(parts[0]))
                    && (parts[1] == "" || parts[1] == ".." || ExtendedDateTime.ExtendedDateTimePattern.IsMatch(parts[1])))
                {
                    range = new ExtendedDateRange()
                    {
                        Type = DateRangeType.Period,
                        Start = parts[0] == ".." || parts[0] == "" 
                            ? new ExtendedDateTime() 
                            : ExtendedDateTime.Parse(parts[0]),
                        End = parts[1] == ".." || parts[1] == ""
                            ? new ExtendedDateTime() 
                            : ExtendedDateTime.Parse(parts[1])
                    };
                }
                else if (ExtendedDateTime.TryParse(value, provider, out var single))
                {
                    range = new ExtendedDateRange()
                    {
                        Type = DateRangeType.Date,
                        Start = single,
                        End = single
                    };
                }
                else
                {
                    range = default;
                    return false;
                }
            }
            return true;
        }

        public override bool Equals(object obj)
        {
            if (obj is ExtendedDateRange range)
                return Type == range.Type
                    && Start == range.Start
                    && End == range.End;
            return false;
        }

        public override int GetHashCode()
        {
            return Type.GetHashCode()
                ^ Start.GetHashCode()
                ^ End.GetHashCode();
        }
    }
}
