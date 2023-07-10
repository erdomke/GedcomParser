using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace GedcomParser.Model
{
  internal class ResolvedEvent
  {
    public List<Individual> Primary { get; } = new List<Individual>();

    public FamilyLinkType PrimaryRole { get; set; }

    public List<Individual> Secondary { get; } = new List<Individual>();

    public Event Event { get; }

    public List<Event> Related { get; } = new List<Event>();

    public ResolvedEvent(Event eventObj)
    {
      Event = eventObj;
    }

    public void Description(HtmlTextWriter html, bool includeDate)
    {
      if (Event.Type == EventType.Birth)
      {
        AddNames(html, Primary, NameForm.All, default);
        html.WriteString(" was born");
        AddDate(html, Event.Date, includeDate);
        if (Secondary.Count > 0)
        {
          html.WriteString(" to ");
          AddNames(html, Secondary, NameForm.Short, Event.Date);
        }
        AddPlace(html, Event);
      }
      else if (Event.Type == EventType.Death)
      {
        AddNames(html, Primary, NameForm.Full, Event.Date);
        html.WriteString(" died");
        AddDate(html, Event.Date, includeDate);
        if (Event.Attributes.TryGetValue("Cause", out var cause))
        {
          html.WriteString(" of ");
          html.WriteString(cause);
        }
        AddPlace(html, Event);
      }
      else if (Event.Type == EventType.Degree)
      {
        AddNames(html, Primary, NameForm.Short, Event.Date);
        html.WriteString(" graduated");
        if (Event.Attributes.TryGetValue("Degree", out var degree))
        {
          html.WriteString(" with a ");
          html.WriteString(degree);
        }
        AddPlace(html, Event, "from");
        AddDate(html, Event.Date, includeDate);
      }
      else if (Event.Type == EventType.Occupation)
      {
        AddNames(html, Primary, NameForm.Short, default);
        html.WriteString(" worked");
        if (Event.Attributes.TryGetValue("Occupation", out var occupation))
        {
          html.WriteString(" as a ");
          html.WriteString(occupation);
        }
        AddDate(html, Event.Date, includeDate, Primary.First());
        AddPlace(html, Event);
      }
      else if (Event.Type == EventType.Marriage)
      {
        AddNames(html, Primary, NameForm.Full, Event.Date);
        html.WriteString(" were married");
        AddDate(html, Event.Date, includeDate);
        AddPlace(html, Event);
      }
      else if (string.Equals(Event.TypeString, "Diagnosis", StringComparison.OrdinalIgnoreCase))
      {
        AddNames(html, Primary, NameForm.Short, Event.Date);
        html.WriteString(" was diagnosed");
        if (Event.Attributes.TryGetValue("Diagnosis", out var diagnosis))
        {
          html.WriteString(" with ");
          html.WriteString(diagnosis);
        }
        AddDate(html, Event.Date, includeDate);
      }
      else if (Event.Type == EventType.Residence)
      {
        AddNames(html, Primary, NameForm.Short, default);
        html.WriteString(" resided");
        AddPlace(html, Event);
        AddDate(html, Event.Date, includeDate);
      }
      else if (Event.Type == EventType.Baptism)
      {
        AddNames(html, Primary, NameForm.Short, Event.Date);
        html.WriteString(" was baptised");
        AddPlace(html, Event);
        AddDate(html, Event.Date, includeDate);
      }
      else if (Event.Type == EventType.Confirmation)
      {
        AddNames(html, Primary, NameForm.Short, Event.Date);
        html.WriteString(" underwent confirmation");
        AddPlace(html, Event);
        AddDate(html, Event.Date, includeDate);
      }
      else
      {
        html.WriteString(Event.TypeString ?? Event.Type.ToString());
        html.WriteString(" of ");
        AddNames(html, Primary, NameForm.Short, default);
        AddDate(html, Event.Date, includeDate);
        AddPlace(html, Event);
      }

      html.WriteString(".");

      if (Event.Attributes.TryGetValue("Weight", out var weight))
      {
        var pronoun = Primary.Count == 1 ? Primary[0].Pronoun() : "they";
        html.WriteString(" "
          + CultureInfo.InvariantCulture.TextInfo.ToTitleCase(pronoun)
          + " weighed " + weight + ".");
      }

      foreach (var related in Related)
      {
        if (related.Type == EventType.Burial
          && (related.Date.HasValue || related.Place != Event.Place))
        {
          var pronoun = Primary.Count == 1 ? Primary[0].Pronoun() : "they";
          html.WriteString(" "
            + CultureInfo.InvariantCulture.TextInfo.ToTitleCase(pronoun)
            + (pronoun == "they" ? " were " : " was ")
            + "buried");
          AddDate(html, related.Date, includeDate);
          AddPlace(html, related);
          html.WriteString(".");
        }
      }
    }

    internal static void AddPlace(HtmlTextWriter html, Event ev, string word = "at")
    {
      if (!string.IsNullOrEmpty(ev.Organization?.Name))
      {
        html.WriteString(" " + word + " ");
        word = "at";
        html.WriteString(ev.Organization.Name);
      }

      var place = ev.Place ?? ev.Organization?.Place;
      if (!string.IsNullOrEmpty(place?.Names.FirstOrDefault()?.Name))
      {
        html.WriteString(" " + word + " ");
        html.WriteString(place.Names.First().Name);
      }
    }

    internal const string DateFormat = "yyyy MMM dd";

    internal static void AddDate(HtmlTextWriter html, ExtendedDateRange date, bool includeDate, Individual ageIndividual = null)
    {
      if (!includeDate || !date.HasValue)
        return;

      string RenderDate(ExtendedDateRange date, bool start)
      {
        var result = (start ? date.Start : date.End).ToString(DateFormat);
        if (ageIndividual != null)
          result += GetAge(ageIndividual, date, !start);
        return result;
      };

      if (date.Type == DateRangeType.Range)
      {
        if (date.Start.HasValue && date.End.HasValue)
          html.WriteString(" between " + RenderDate(date, true) + " and " + RenderDate(date, false));
        else if (date.Start.HasValue)
          html.WriteString(" after " + RenderDate(date, true));
        else if (date.End.HasValue)
          html.WriteString(" before " + RenderDate(date, false));
      }
      else if (date.Type == DateRangeType.Period)
      {
        if (date.Start.HasValue && date.End.HasValue)
          html.WriteString(" from " + RenderDate(date, true) + " to " + RenderDate(date, false));
        else if (date.Start.HasValue)
          html.WriteString(" from " + RenderDate(date, true));
        else if (date.End.HasValue)
          html.WriteString(" until " + RenderDate(date, false));
      }
      else
      {
        html.WriteString((date.Start.Day.HasValue ? " on " : " in ") + RenderDate(date, true));
      }
    }

    internal static void AddNames(HtmlTextWriter html, IEnumerable<Individual> individuals, NameForm nameForm, ExtendedDateRange ageDate)
    {
      var first = true;
      foreach (var i in individuals)
      {
        if (first)
          first = false;
        else
          html.WriteString(" and ");
        html.WriteStartElement("a");
        html.WriteAttributeString("href", "#" + i.Id.Primary);
        if (nameForm == NameForm.Full)
        {
          html.WriteString(i.Name.Name);
        }
        else if (nameForm == NameForm.Short)
        {
          var name = i.Names.OrderBy(n => n.Type == NameType.Birth ? 0 : 1).FirstOrDefault();
          html.WriteString(name.Nickname ?? name.Name.Remaining ?? name.GivenName);
        }
        else // All
        {
          html.WriteString(i.Name.Name);
          var aliases = i.Names.Where(n => n.Type != NameType.Married)
            .SelectMany(n => new[] { n.Name.Remaining, n.Nickname })
            .Where(n => !string.IsNullOrEmpty(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
          aliases.Remove(i.Name.Remaining);
          if (aliases.Count > 0)
          {
            html.WriteString(" (");
            var firstAlias = true;
            foreach (var alias in aliases)
            {
              if (firstAlias)
                firstAlias = false;
              else
                html.WriteString(", ");
              html.WriteString(alias);
            }
            html.WriteString(")");
          }
        }

        html.WriteEndElement();
        if (ageDate.HasValue)
          html.WriteString(GetAge(i, ageDate, false));
      }
    }

    private static string GetAge(Individual i, ExtendedDateRange dateTime, bool useMax)
    {
      var birth = i.Events.FirstOrDefault(e => e.Type == EventType.Birth && e.Date.HasValue);
      if (birth != null && dateTime.HasValue
        && birth.Date.TryGetDiff(dateTime, out var minimum, out var maximum))
      {
        var span = useMax ? maximum : minimum;
        if (span.FullMonths < 1)
          return " (" + span.Days + " days)";
        else if (span.Years < 2)
          return " (" + span.FullMonths + " months)";
        else
          return " (" + span.Years + " years)";
      }
      return string.Empty;
    }

    internal enum NameForm
    {
      Full,
      Short,
      All
    }
  }
}
