using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace GedcomParser.Model
{
  [DebuggerDisplay("{Name}")]
  public class Individual : IPrimaryObject, IHasEvents
  {
    public Identifiers Id { get; } = new Identifiers();
    public string DuplicateOf { get; set; }

    public Media Picture { get; set; }
    public Sex Sex { get; set; }
    public Species Species { get; set; }
    public PersonName Name => Names.OrderBy(n => n.Type == NameType.Birth ? 0 : 1).FirstOrDefault()?.Name ?? default;
    public ExtendedDateRange BirthDate => Events.FirstOrDefault(e => e.Type == EventType.Birth)?.Date ?? default;
    public ExtendedDateRange FamilyInferredBirthDate { get; set; }
    public ExtendedDateRange DeathDate => Events.FirstOrDefault(e => e.Type == EventType.Death)?.Date ?? default;
    public List<IndividualName> Names { get; } = new List<IndividualName>();
    public List<Event> Events { get; } = new List<Event>();

    public Dictionary<string, string> Attributes { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public List<Citation> Citations { get; } = new List<Citation>();
    public List<Link> Links { get; } = new List<Link>();
    public List<Media> Media { get; } = new List<Media>();
    public List<Note> Notes { get; } = new List<Note>();

    public string DateString
    {
      get
      {
        var result = BirthDate.ToString("s") + " - ";
        var deathEvent = Events.FirstOrDefault(e => e.Type == EventType.Death);
        if (deathEvent == null)
        {
          result += "?";
        }
        else
        {
          if (deathEvent.Date.HasValue)
            result += deathEvent.Date.ToString("s");
          else
            result += "Deceased";
        }
        return result;
      }
    }

    public string Pronoun()
    {
      switch (Sex)
      {
        case Sex.Male:
          return "he";
        case Sex.Female:
          return "she";
        default:
          return "they";
      }
    }

    public void BuildEqualityString(StringBuilder builder, Database db)
    {
      builder.Append(Sex.ToString());
      foreach (var name in Names)
        builder.Append(name.Name);
      foreach (var e in Events)
        builder.Append(e.TypeString ?? e.Type.ToString()).Append(e.Date.ToString("s"));
      Utilities.BuildEqualityString(this, builder);
    }

    public string GetPreferredId(Database db)
    {
      var builder = new StringBuilder();
      var name = Names.FirstOrDefault().Name;
      Utilities.AddFirstLetters(name.Surname, 10, builder);
      Utilities.AddFirstLetters(name.Remaining, 10, builder);
      if (BirthDate.TryGetRange(out var start, out var _) && start.HasValue)
        builder.Append(start.Value.ToString("yyyyMMdd"));
      return builder.ToString();
    }
  }
}
