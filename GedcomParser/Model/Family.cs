using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GedcomParser.Model
{
  public class Family : IPrimaryObject, IHasEvents
  {
    public Identifiers Id { get; } = new Identifiers();
    public string DuplicateOf { get; set; }
    public List<Event> Events { get; } = new List<Event>();
    public FamilyType Type { get; set; }

    public Dictionary<string, string> Attributes { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public List<Citation> Citations { get; } = new List<Citation>();
    public List<Link> Links { get; } = new List<Link>();
    public List<Media> Media { get; } = new List<Media>();
    public List<Note> Notes { get; } = new List<Note>();

    public void BuildEqualityString(StringBuilder builder, Database db)
    {
      builder.Append(Type.ToString());
      foreach (var memberName in db.FamilyLinks(this, FamilyLinkType.Other)
        .Select(l => db.TryGetValue(l.Individual, out Individual individual) ? individual.Name : default(PersonName))
        .Where(n => n.Name.Length > 0)
        .Distinct())
        builder.Append(memberName);
      foreach (var e in Events)
        builder.Append(e.TypeString ?? e.Type.ToString()).Append(e.Date.ToString("s"));
      Utilities.BuildEqualityString(this, builder);
    }

    public string GetPreferredId(Database db)
    {
      var builder = new StringBuilder("F_");
      foreach (var parentName in db.FamilyLinks(this, FamilyLinkType.Parent)
        .Select(l => db.TryGetValue(l.Individual, out Individual individual) ? individual.Name : default(PersonName))
        .Where(n => n.Name.Length > 0)
        .Select(n => n.Surname ?? n.Remaining)
        .Distinct()
        .Take(2))
        Utilities.AddFirstLetters(parentName, 10, builder);
      var marriage = Events.FirstOrDefault(e => e.Type == EventType.Marriage);
      if (marriage != null && marriage.Date.TryGetRange(out var start, out var _) && start.HasValue)
        builder.Append(start.Value.ToString("yyyy"));
      return builder.ToString();
    }
  }
}
