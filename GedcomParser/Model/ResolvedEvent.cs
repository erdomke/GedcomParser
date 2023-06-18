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
      }
      else if (Event.Type == EventType.Marriage)
      {
        AddNames(html, Primary, true);
        html.WriteString(" were married");
      }
      else
      {
        html.WriteString(Event.TypeString ?? Event.Type.ToString());
        html.WriteString(" of ");
        AddNames(html, Primary, false);
      }

      if (!string.IsNullOrEmpty(Event.Place?.Names.FirstOrDefault()?.Name))
      {
        html.WriteString(" at ");
        html.WriteString(Event.Place.Names.First().Name);
      }
      html.WriteString(".");

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
            html.WriteString(" (" + minimum.Years + " years old)");
          }
        }
      }
    }
  }
}
