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

    public string Description()
    {
      var builder = new StringBuilder();
      if (Event.Type == EventType.Birth)
      {
        builder.Append(string.Join(" and ", Primary.Select(i => i.Name.Name)));
        builder.Append(" was born");
        if (Secondary.Count > 0)
        {
          builder.Append(" to ");
          AddNamesWithAge(builder, Secondary);
        }
      }
      else if (Event.Type == EventType.Death)
      {
        AddNamesWithAge(builder, Primary);
        builder.Append(" died");
      }
      else if (Event.Type == EventType.Marriage)
      {
        AddNamesWithAge(builder, Primary);
        builder.Append(" were married");
      }
      else
      {
        builder.Append(Event.TypeString ?? Event.Type.ToString());
        builder.Append(" of ");
        builder.Append(string.Join(" and ", Primary.Select(i => i.Name.Name)));
      }

      if (!string.IsNullOrEmpty(Event.Place?.Names.FirstOrDefault()?.Name))
        builder.Append(" at ").Append(Event.Place.Names.First().Name);
      builder.Append('.');

      foreach (var related in Related)
      {
        if (related.Type == EventType.Burial)
        {
          var pronoun = Primary.Count == 1 ? Primary[0].Pronoun() : "they";
          builder
            .Append(' ')
            .Append(CultureInfo.InvariantCulture.TextInfo.ToTitleCase(pronoun))
            .Append(pronoun == "they" ? " were " : " was ")
            .Append("buried");
          if (related.Date.HasValue)
            builder.Append(" on ").Append(related.Date.ToString("yyyy MMM d"));
          if (!string.IsNullOrEmpty(related.Place?.Names.FirstOrDefault()?.Name))
            builder.Append(" at ").Append(related.Place.Names.First().Name);
          builder.Append('.');
        }
      }

      return builder.ToString();
    }

    private void AddNamesWithAge(StringBuilder builder, IEnumerable<Individual> individuals)
    {
      var first = true;
      foreach (var i in individuals)
      {
        if (first)
          first = false;
        else
          builder.Append(" and ");
        builder.Append(i.Name.Name.ToString());
        var birth = i.Events.FirstOrDefault(e => e.Type == EventType.Birth && e.Date.HasValue);
        if (birth != null && Event.Date.HasValue
          && birth.Date.TryGetDiff(Event.Date, out var minimum, out var _))
        {
          builder.Append(" (").Append(minimum.Years).Append(" years old)");
        }
      }
    }
  }
}
