﻿using Microsoft.VisualBasic;
using SixLabors.Fonts;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;

namespace GedcomParser
{
  /// <summary>
  /// This class can handle parsing and representing <a href="https://gedcom.io/specifications/FamilySearchGEDCOMv7.pdf">GEDCOM dates</a>.
  /// It can also handle <a href="https://www.loc.gov/standards/datetime/">extended dates</a> level 0 along with parts of level 1 and 2.
  /// </summary>
  public struct ExtendedDateTime : IComparable<ExtendedDateTime>, IFormattable
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

    /// <summary>
    /// A statement regarding the certainty of the date.
    /// </summary>
    public DateCertainty Certainty
    {
      get => (DateCertainty)(_data & 0b11);
      private set => _data = (_data & ~0b11UL) | ((ulong)value & 0b11UL);
    }

    public ExtendedDateTime WithCertainty(DateCertainty certainty)
    {
      return new ExtendedDateTime()
      {
        _data = _data,
        Certainty = certainty
      };
    }

    /// <summary>
    /// The calendar to which the date belongs.
    /// </summary>
    public DateCalendar Calendar
    {
      get => (DateCalendar)((_data >> 2) & 0b111);
      private set => _data = (_data & ~0b11100UL) | (((ulong)value & 0b111UL) << 2);
    }

    /// <summary>
    /// Gets a value that indicates whether the time represented by this instance is
    /// based on local time, Coordinated Universal Time (UTC), or neither.
    /// </summary>
    /// <returns>
    /// One of the enumeration values that indicates what the current time represents.
    /// The default is <c>System.DateTimeKind.Unspecified</c>.
    /// </returns>
    public DateTimeKind Kind
    {
      get => (DateTimeKind)((_data >> 5) & 0b11);
      private set => _data = (_data & ~0b1100000UL) | (((ulong)value & 0b11UL) << 5);
    }

    /// <summary>
    /// Gets the year component of the date represented by this instance.
    /// </summary>
    /// <returns>
    /// The year, between -32768 and 32767.
    /// </returns>
    public int Year
    {
      get
      {
        return (short)((_data >> 48) & 0xFFFF);
      }
      set
      {
        var stored = (ulong)((short)value) & 0xFFFF;
        _data = (_data & ~(0xFFFFUL << 48))
            | stored << 48;
      }
    }

    /// <summary>
    /// Whether this date/time has a value or not
    /// </summary>
    public bool HasValue => Precision != YearPrecision.NoValue;

    private YearPrecision Precision
    {
      get => (YearPrecision)((_data >> 46) & 0b11);
      set => _data = (_data & ~(0b11UL << 46)) | (((ulong)value & 0b11UL) << 46);
    }

    /// <summary>
    /// Gets the month component of the date represented by this instance.
    /// </summary>
    /// <returns>
    /// The month component (if known), expressed as a value between 1 and the maximum number of months in the calendar.
    /// </returns>
    public int? Month
    {
      get
      {
        var stored = (int)((_data >> 40) & 0b111111);
        if (stored <= 0 || stored > 20)
          return null;
        return stored;
      }
      set
      {
        var stored = (ulong)(value.HasValue ? value.Value : 0) & 0b111111;
        _data = (_data & ~(0b111111UL << 40))
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

    public ExtendedDateTime(DateTime dateTime)
    {
      _data = 0;

      Calendar = DateCalendar.Gregorian;
      Certainty = DateCertainty.Known;
      Kind = dateTime.Kind;
      Precision = YearPrecision.Year;
      Year = dateTime.Year;
      Month = dateTime.Month;
      Day = dateTime.Day;
      Hour = dateTime.Hour;
      Minute = dateTime.Minute;
      Second = dateTime.Second;
      Millisecond = dateTime.Millisecond;
    }

    public ExtendedDateTime AddYears(int years)
    {
      if (!HasValue)
        return this;

      var result = new ExtendedDateTime()
      {
        _data = _data
      };
      result.Year = Year + years;
      return result;
    }

    public ExtendedDateTime AddMonths(int months)
    {
      if (!HasValue)
        return this;

      var years = months / 12;
      months = months % 12;

      var result = new ExtendedDateTime()
      {
        _data = _data
      };
      result.Year = Year + years;
      var newMonth = Month + months;
      if (newMonth > 12)
      {
        result.Year++;
        result.Month = newMonth - 12;
      }
      else if (newMonth < 1)
      {
        result.Year--;
        result.Month = 12 + newMonth;
      }
      else
      {
        result.Month = newMonth;
      }
      return result;
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
          value = new DateTime(Year, Month.Value, Day.Value, Hour ?? 0, Minute ?? 0, Second ?? 0, Millisecond ?? 0, Kind);
        else
          value = new DateTime(Year, Month.Value, Day.Value, Hour ?? 0, Minute ?? 0, Second ?? 0, Millisecond ?? 0, calendar, Kind);
        return true;
      }
      else
      {
        value = default;
        return false;
      }
    }

    public bool TryGetRange(out DateTime start, out DateTime end)
    {
      if (TryGetCalendar(out var calendar)
          && Year > 0)
      {
        start = new DateTime(Year
            , Month ?? 1
            , Day ?? 1
            , Hour ?? 0
            , Minute ?? 0
            , Second ?? 0
            , Millisecond ?? 0
            , calendar
            , Kind);

        var endMonth = Month ?? calendar.GetMonthsInYear(Year);
        end = new DateTime(Year
            , endMonth
            , Day ?? calendar.GetDaysInMonth(Year, endMonth)
            , Hour ?? 23
            , Minute ?? 59
            , Second ?? 59
            , Millisecond ?? 999
            , calendar
            , Kind);
        return true;
      }
      else
      {
        start = default;
        end = default;
        return false;
      }
    }

    private string ToSortString()
    {
      if (Precision == YearPrecision.NoValue)
        return "";

      return (Year - short.MinValue).ToString("D5")
          + "-" + (Month ?? 1).ToString("D2")
          + "-" + (Day ?? 1).ToString("D2")
          + "T" + (Hour ?? 0).ToString("D2")
          + ":" + (Minute ?? 0).ToString("D2")
          + ":" + (Second ?? 0).ToString("D2")
          + "." + (Millisecond ?? 0).ToString("D3");
    }

    public override string ToString()
    {
      if (Precision == YearPrecision.NoValue)
        return "?";

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
      if (Year > 0)
        result += Year;
      else
        result += (Year - 1) * -1 + " BCE";
      return result;
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
        var parts = default(List<string>);

        var isSortable = format == "s";
        if (isSortable || format == "u")
          parts = new List<string>() { "yyyy", "`-", "MM", "`-", "dd", (format == "s" ? "`T" : "` "), "HH", "`:", "mm", "`:", "ss" };
        else
          parts = SpecifierParts(format, formatProvider).ToList();

        var precision = DateTimePrecision.Year;
        if (Millisecond.HasValue)
          precision = DateTimePrecision.Millisecond;
        else if (Second.HasValue)
          precision = DateTimePrecision.Second;
        else if (Minute.HasValue)
          precision = DateTimePrecision.Minute;
        else if (Hour.HasValue)
          precision = DateTimePrecision.Hour;
        else if (Day.HasValue)
          precision = DateTimePrecision.Day;
        else if (Month.HasValue)
          precision = DateTimePrecision.Month;

        var lastValid = parts.Count - 1;
        while (lastValid >= 0 && SpecifierPrecision(parts[lastValid]) > precision)
        {
          lastValid--;
          while (lastValid >= 0 && SpecifierPrecision(parts[lastValid]) == DateTimePrecision.Other)
            lastValid--;
        }
        parts = parts.Take(lastValid + 1).ToList();

        if (format == "u" && precision >= DateTimePrecision.Second)
          parts.Add("`Z");
        if (isSortable && precision == DateTimePrecision.Year && Math.Abs(Year) >= 10000)
          parts.Insert(0, "`Y");
        if (isSortable && Certainty != DateCertainty.Known)
          parts.Add("`~");

        var builder = new StringBuilder();
        var dateFormat = DateTimeFormat(formatProvider);
        foreach (var part in parts)
        {
          switch (part)
          {
            case "d":
            case "dd":
              builder.Append(Day.HasValue ? Day.Value.ToString("D" + part.Length) : new string('X', part.Length));
              break;
            case "g":
            case "gg":
              if (Year <= 0)
                builder.Append("B.C.");
              else
                builder.Append(dateFormat.GetEraName(0));
              break;
            case "M":
            case "MM":
              builder.Append(Month.HasValue ? Month.Value.ToString("D" + part.Length) : new string('X', part.Length));
              break;
            case "MMM":
              builder.Append(Month.HasValue ? dateFormat.GetAbbreviatedMonthName(Month.Value) : "XXX");
              break;
            case "MMMM":
              builder.Append(Month.HasValue ? dateFormat.GetMonthName(Month.Value) : "XXXX");
              break;
            case "y":
            case "yy":
            case "yyy":
            case "yyyy":
            case "yyyyy":
              var yearString = (part.Length <= 2 ? (Year % 100) : Year).ToString("D" + part.Length);
              var mask = new string('X', (int)Precision - 1);
              if (mask.Length > 0
                  && int.TryParse(yearString.Substring(yearString.Length - mask.Length), out var maskInt)
                  && maskInt != 0)
              {
                builder.Append(yearString).Append('S').Append(mask.Length);
              }
              else
              {
                builder
                    .Append(yearString.Substring(0, yearString.Length - mask.Length))
                    .Append(mask);
              }
              break;
            case "H":
            case "HH":
              builder.Append(Hour.HasValue ? Hour.Value.ToString("D" + part.Length) : new string('X', part.Length));
              break;
            case "m":
            case "mm":
              builder.Append(Minute.HasValue ? Minute.Value.ToString("D" + part.Length) : new string('X', part.Length));
              break;
            case "s":
            case "ss":
              builder.Append(Second.HasValue ? Second.Value.ToString("D" + part.Length) : new string('X', part.Length));
              break;
            case "f":
            case "ff":
            case "fff":
              builder.Append(Millisecond.HasValue ? (Millisecond.Value % (int)Math.Pow(10, part.Length)).ToString("D" + part.Length) : new string('X', part.Length));
              break;
            default:
              if (part.StartsWith("`"))
                builder.Append(part.Substring(1));
              else
                throw new InvalidOperationException($"Invalid format string {format}");
              break;
          }
        }
        return builder.ToString();
      }
    }

    private DateTimePrecision SpecifierPrecision(string part)
    {
      switch (part)
      {
        case "d":
        case "dd":
          return DateTimePrecision.Day;
        case "M":
        case "MM":
        case "MMM":
        case "MMMM":
          return DateTimePrecision.Month;
        case "y":
        case "yy":
        case "yyy":
        case "yyyy":
        case "yyyyy":
          return DateTimePrecision.Year;
        case "H":
        case "HH":
          return DateTimePrecision.Hour;
        case "m":
        case "mm":
          return DateTimePrecision.Minute;
        case "s":
        case "ss":
          return DateTimePrecision.Second;
        case "f":
        case "ff":
        case "fff":
          return DateTimePrecision.Millisecond;
        default:
          return DateTimePrecision.Other;
      }
    }

    private enum DateTimePrecision
    {
      Other = 0,
      Year = 1,
      Month = 2,
      Day = 3,
      Hour = 4,
      Minute = 5,
      Second = 6,
      Millisecond = 7,
  }

    private IEnumerable<string> SpecifierParts(string format, IFormatProvider formatProvider)
    {
      var index = 0;
      while (TryConsumeSpecifier(format, formatProvider, ref index, out var specifier, out var constant))
      {
        if (specifier == null)
          yield return "`" + constant;
        else
          yield return specifier;
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

    public static ExtendedDateTime Parse(string value)
    {
      if (TryParse(value, out var result))
        return result;
      throw new FormatException($"{value} is not a valid date.");
    }

    public static ExtendedDateTime ParseGedcom(string value)
    {
      if (TryParseGedcom(value, out var result))
        return result;
      throw new FormatException($"{value} is not a valid GEDCOM date.");
    }

    public static bool TryParse(string value, out ExtendedDateTime date)
    {
      return TryParse(value, CultureInfo.CurrentCulture, out date);
    }

    internal static Regex ExtendedDateTimePattern { get; } = new Regex(@"^Y?(?<year>-?\d+X{0,2}(S\d+)?)(-(?<month>\d{2}|XX)(-(?<day>\d{2}|XX)([ T](?<hour>\d{2}|XX)(:(?<minute>\d{2}|XX)(:(?<second>\d{2}|XX))?)?(?<tz>Z|[+-]\d{2}(:\d{2})?)?)?)?)?(?<certainty>[?~%])?$", RegexOptions.Compiled);

    public static bool TryParse(string value, IFormatProvider provider, out ExtendedDateTime date)
    {
      if (string.IsNullOrEmpty(value))
      {
        date = default;
        return false;
      }
      else if (TryParseGedcom(value, out date))
      {
        return true;
      }

      var match = ExtendedDateTimePattern.Match(value);
      if (match.Success)
      {
        date = new ExtendedDateTime()
        {
          Calendar = DateCalendar.Gregorian,
          Certainty = match.Groups["certainty"].Success ? DateCertainty.About : DateCertainty.Known,
          Kind = match.Groups["tz"].Success && match.Groups["tz"].Value == "Z" ? DateTimeKind.Utc : DateTimeKind.Local,
          Precision = YearPrecision.Year
        };

        var yearParts = match.Groups["year"].Value.Split('S');
        if (short.TryParse(yearParts[0].Replace('X', '0'), out var year))
        {
          date.Year = year;
          date.Precision = (YearPrecision)(yearParts[0].Length - yearParts[0].Replace("X", "").Length + 1);
        }
        else
        {
          return false;
        }
        if (date.Precision == YearPrecision.Year
            && yearParts.Length > 1
            && int.TryParse(yearParts[1], out var sigFigs))
        {
          date.Precision = (YearPrecision)Math.Min(yearParts[0].TrimStart('-').Length - sigFigs, 2) + 1;
        }
        if (match.Groups["month"].Success && int.TryParse(match.Groups["month"].Value, out var month))
          date.Month = month;
        if (match.Groups["day"].Success && int.TryParse(match.Groups["day"].Value, out var day))
        {
          date.Day = day;
          if (date.Day < 1 || date.Day > 36)
            return false;
        }
        if (match.Groups["hour"].Success && int.TryParse(match.Groups["hour"].Value, out var hour))
        {
          date.Hour = hour;
          if (date.Hour >= 24)
            return false;
        }
        if (match.Groups["minute"].Success && int.TryParse(match.Groups["minute"].Value, out var minute))
        {
          date.Minute = minute;
          if (date.Minute >= 60)
            return false;
        }
        if (match.Groups["second"].Success && int.TryParse(match.Groups["second"].Value, out var second))
        {
          date.Second = second;
          if (date.Second >= 60)
            return false;
        }
        return true;
      }

      var patterns = new HashSet<string>(DateTimeFormat(provider)
          .GetAllDateTimePatterns()
          .Where(p => p.IndexOf("yyy") >= 0))
            {
                "d MMMM yyyy"
            };
      foreach (var pattern in patterns)
      {
        if (DateTime.TryParseExact(value, pattern, provider, DateTimeStyles.None, out var dateTime))
        {
          date = new ExtendedDateTime()
          {
            Calendar = DateCalendar.Gregorian,
            Certainty = DateCertainty.Known,
            Kind = dateTime.Kind,
            Precision = YearPrecision.Year
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

    public static bool TryParseGedcom(string value, out ExtendedDateTime date)
    {
      if (string.IsNullOrEmpty(value))
      {
        date = default;
        return false;
      }

      var parts = value.Split(' ');
      var idx = 0;
      date = new ExtendedDateTime()
      {
        Certainty = ConsumeApproximate(parts, ref idx),
        Calendar = ConsumeCalendar(parts, ref idx),
        Day = ConsumeDay(parts, ref idx),
        Kind = DateTimeKind.Local
      };
      date.Month = ConsumeMonth(parts, date.Calendar, ref idx);
      date.Year = ConsumeYear(parts, ref idx, out var precision);
      date.Precision = precision;
      if (ConsumeEpoch(parts, ref idx) && date.HasValue)
        date.Year = date.Year * -1 + 1;

      if (idx < parts.Length)
      {
        var match = Regex.Match(parts[idx], @"^(?<hour>\d{2}):(?<minute>\d{2})(:(?<second>\d{2})(.(?<fraction>\d+))?)?(?<utc>Z)?$");
        if (match.Success)
        {
          idx++;
          date.Hour = int.Parse(match.Groups["hour"].Value);
          if (date.Hour >= 24)
            return false;
          date.Minute = int.Parse(match.Groups["minute"].Value);
          if (date.Minute >= 60)
            return false;
          if (match.Groups["second"].Success)
          {
            date.Second = int.Parse(match.Groups["second"].Value);
            if (date.Second >= 60)
              return false;
          }
          if (match.Groups["fraction"].Success)
          {
            var fraction = match.Groups["fraction"].Value;
            if (fraction.Length > 3)
              fraction = fraction.Substring(0, 3);
            else if (fraction.Length < 3)
              fraction += new string('0', 3 - fraction.Length);
            date.Millisecond = int.Parse(fraction);
          }
          if (match.Groups["utc"].Success)
            date.Kind = DateTimeKind.Utc;
        }
      }

      return idx >= parts.Length && date.HasValue;
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

    private static int ConsumeYear(string[] parts, ref int idx, out YearPrecision precision)
    {
      if (idx < parts.Length && short.TryParse(parts[idx], out var year))
      {
        idx++;
        precision = YearPrecision.Year;
        return year;
      }
      else
      {
        precision = YearPrecision.NoValue;
        return 0;
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

    private enum YearPrecision
    {
      NoValue = 0,
      Year = 1,
      Decade = 2,
      Century = 3
    }

    public override bool Equals(object obj)
    {
      if (obj is ExtendedDateTime extended)
        return _data == extended._data;
      return false;
    }

    public override int GetHashCode()
    {
      return _data.GetHashCode();
    }

    public int CompareTo([AllowNull] ExtendedDateTime other)
    {
      return ToSortString().CompareTo(other.ToSortString());
    }

    public static bool operator ==(ExtendedDateTime x, ExtendedDateTime y)
    {
      return x.Equals(y);
    }

    public static bool operator !=(ExtendedDateTime x, ExtendedDateTime y)
    {
      return !x.Equals(y);
    }

    public static bool operator >(ExtendedDateTime x, ExtendedDateTime y)
    {
      return x.CompareTo(y) > 0;
    }

    public static bool operator >=(ExtendedDateTime x, ExtendedDateTime y)
    {
      return x.CompareTo(y) >= 0;
    }

    public static bool operator <(ExtendedDateTime x, ExtendedDateTime y)
    {
      return x.CompareTo(y) < 0;
    }

    public static bool operator <=(ExtendedDateTime x, ExtendedDateTime y)
    {
      return x.CompareTo(y) <= 0;
    }
  }
}
