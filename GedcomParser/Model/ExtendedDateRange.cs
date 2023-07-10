using System;
using System.Globalization;

namespace GedcomParser
{
  public struct ExtendedDateRange : IFormattable, IComparable<ExtendedDateRange>
  {
    public bool HasValue => Type != DateRangeType.Unknown;
    public DateRangeType Type { get; private set; }
    public ExtendedDateTime Start { get; private set; }
    public ExtendedDateTime End { get; private set; }

    public ExtendedDateRange(ExtendedDateTime date)
    {
      Type = DateRangeType.Date;
      Start = date;
      End = date;
    }

    public ExtendedDateRange(ExtendedDateTime start, ExtendedDateTime end)
    {
      Type = DateRangeType.Range;
      Start = start;
      End = end;
    }

    public ExtendedDateRange(ExtendedDateTime start, ExtendedDateTime end, DateRangeType type)
    {
      Type = type;
      Start = start;
      End = end;
    }

    public bool InRange(ExtendedDateRange range)
    {
      if (Start.HasValue && (range.Start.HasValue ? range.Start : range.End) < Start)
        return false;
      else if (End.HasValue && (range.End.HasValue ? range.End : range.Start) > End)
        return false;
      return true;
    }

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

    public bool TryGetDiff(ExtendedDateRange endRange, out DateTimeSpan minimum, out DateTimeSpan maximum)
    {
      if (TryGetRange(out var startMin, out var startMax)
          && endRange.TryGetRange(out var endMin, out var endMax))
      {
        minimum = new DateTimeSpan((startMax ?? startMin).Value, (endMin ?? endMax).Value);
        maximum = new DateTimeSpan((startMin ?? startMax).Value, (endMax ?? endMin).Value);
        return true;
      }
      else
      {
        minimum = DateTimeSpan.Zero;
        maximum = DateTimeSpan.Zero;
        return false;
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
        if (Type == DateRangeType.Range)
        {
          if (!Start.HasValue)
            return "< " + End.ToString(format, formatProvider);
          else if (!End.HasValue)
            return "> " + Start.ToString(format, formatProvider);
        }
        return (!Start.HasValue ? ".." : Start.ToString(format, formatProvider))
            + "/"
            + (!End.HasValue ? ".." : End.ToString(format, formatProvider));
      }
      else
      {
        if (Type == DateRangeType.Period)
        {
          if (Start.HasValue && End.HasValue)
            return "FROM " + Start.ToString(format, formatProvider)
                + " TO " + End.ToString(format, formatProvider);
          else if (Start.HasValue)
            return "FROM " + Start.ToString(format, formatProvider);
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
        || value.StartsWith("AFTER ", StringComparison.OrdinalIgnoreCase)
        || value.StartsWith("> ", StringComparison.OrdinalIgnoreCase))
      {
        var startIdx = value.IndexOf(' ') + 1;
        range = new ExtendedDateRange() { Type = DateRangeType.Range };
        if (ExtendedDateTime.TryParse(value.Substring(startIdx), provider, out var start))
          range.Start = start;
        else
          return false;
      }
      else if (value.StartsWith("BEF ", StringComparison.OrdinalIgnoreCase)
        || value.StartsWith("BEFORE ", StringComparison.OrdinalIgnoreCase)
        || value.StartsWith("< ", StringComparison.OrdinalIgnoreCase))
      {
        var startIdx = value.IndexOf(' ') + 1;
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

    public int CompareTo(ExtendedDateRange other)
    {
      if (this.Equals(other))
        return 0;
      var thisDate = this.Start.HasValue ? this.Start : this.End;
      var otherDate = other.Start.HasValue ? other.Start : other.End;

      var result = thisDate.CompareTo(otherDate);
      if (result == 0)
        return this.OpenEndedOffset().CompareTo(other.OpenEndedOffset());
      return result;
    }

    private int OpenEndedOffset()
    {
      if (this.Type == DateRangeType.Period
        || this.Type == DateRangeType.Range)
      {
        if (!this.End.HasValue)
          return 1;
        if (!this.Start.HasValue)
          return -1;
      }
      return 0;
    }


    public static bool operator ==(ExtendedDateRange x, ExtendedDateRange y)
    {
      return x.Equals(y);
    }

    public static bool operator !=(ExtendedDateRange x, ExtendedDateRange y)
    {
      return !x.Equals(y);
    }

    public static bool operator >(ExtendedDateRange x, ExtendedDateRange y)
    {
      return x.CompareTo(y) > 0;
    }

    public static bool operator >=(ExtendedDateRange x, ExtendedDateRange y)
    {
      return x.CompareTo(y) >= 0;
    }

    public static bool operator <(ExtendedDateRange x, ExtendedDateRange y)
    {
      return x.CompareTo(y) < 0;
    }

    public static bool operator <=(ExtendedDateRange x, ExtendedDateRange y)
    {
      return x.CompareTo(y) <= 0;
    }
  }
}
