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

    public void Description(HtmlTextWriter html)
    {
      if (Event.Type == EventType.Birth)
      {
        AddNames(html, Primary, false);
        html.WriteString(" was born");
        if (Secondary.Count > 0)
        {
          html.WriteString(" to ");
          AddNames(html, Secondary, true);
        }
      }
      else if (Event.Type == EventType.Death)
      {
        AddNames(html, Primary, true);
        html.WriteString(" died");
        if (Event.Attributes.TryGetValue("Cause", out var cause))
        {
          html.WriteString(" of ");
          html.WriteString(cause);
        }
      }
      else if (Event.Type == EventType.Degree)
      {
        AddNames(html, Primary, true);
        html.WriteString(" graduated");
        if (Event.Attributes.TryGetValue("Degree", out var degree))
        {
          html.WriteString(" with a ");
          html.WriteString(degree);
        }
      }
      else if (Event.Type == EventType.Occupation)
      {
        AddNames(html, Primary, false);
        html.WriteString(" worked");
        if (Event.Attributes.TryGetValue("Occupation", out var occupation))
        {
          html.WriteString(" as a ");
          html.WriteString(occupation);

          var birth = Primary.First().Events.FirstOrDefault(e => e.Type == EventType.Birth && e.Date.HasValue);
          if (birth != null && Event.Date.HasValue
            && birth.Date.TryGetDiff(Event.Date, out var minimum, out var maximum))
          {
            html.WriteString(" from " + minimum.Years + " years old");
            if ((Event.Date.Type == DateRangeType.Period || Event.Date.Type == DateRangeType.Range)
              && maximum.Years > minimum.Years)
            {
              html.WriteString(" to " + maximum.Years + " years old");
            }
          }
        }
      }
      else if (Event.Type == EventType.Marriage)
      {
        AddNames(html, Primary, true);
        html.WriteString(" were married");
      }
      else if (string.Equals(Event.TypeString, "Diagnosis", StringComparison.OrdinalIgnoreCase))
      {
        AddNames(html, Primary, true);
        html.WriteString(" was diagnosed");
        if (Event.Attributes.TryGetValue("Diagnosis", out var diagnosis))
        {
          html.WriteString(" with ");
          html.WriteString(diagnosis);
        }
      }
      else if (Event.Type == EventType.Residence)
      {
        AddNames(html, Primary, true);
        html.WriteString(" resided");
      }
      else
      {
        html.WriteString(Event.TypeString ?? Event.Type.ToString());
        html.WriteString(" of ");
        AddNames(html, Primary, false);
      }

      if (!string.IsNullOrEmpty(Event.Organization?.Name))
      {
        html.WriteString(" at ");
        html.WriteString(Event.Organization.Name);
      }

      var place = Event.Place ?? Event.Organization?.Place;
      if (!string.IsNullOrEmpty(place?.Names.FirstOrDefault()?.Name))
      {
        html.WriteString(" at ");
        html.WriteString(place.Names.First().Name);
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
        if (related.Type == EventType.Burial)
        {
          var pronoun = Primary.Count == 1 ? Primary[0].Pronoun() : "they";
          html.WriteString(" "
            + CultureInfo.InvariantCulture.TextInfo.ToTitleCase(pronoun)
            + (pronoun == "they" ? " were " : " was ")
            + "buried");
          if (related.Date.HasValue)
            html.WriteString(" on " + related.Date.ToString("yyyy MMM d"));
          if (!string.IsNullOrEmpty(related.Place?.Names.FirstOrDefault()?.Name))
            html.WriteString(" at " + related.Place.Names.First().Name);
          html.WriteString(".");
        }
      }
    }

    private void AddNames(HtmlTextWriter html, IEnumerable<Individual> individuals, bool includeAge)
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
        html.WriteString(i.Name.Name.ToString());
        html.WriteEndElement();
        if (includeAge)
        {
          var birth = i.Events.FirstOrDefault(e => e.Type == EventType.Birth && e.Date.HasValue);
          if (birth != null && Event.Date.HasValue
            && birth.Date.TryGetDiff(Event.Date, out var minimum, out var _))
          {
            if (minimum.FullMonths < 1)
              html.WriteString(" (" + minimum.Days + " days old)");
            else if (minimum.Years < 2)
              html.WriteString(" (" + minimum.FullMonths + " months old)");
            else
              html.WriteString(" (" + minimum.Years + " years old)");
          }
        }
      }
    }
  }
}
