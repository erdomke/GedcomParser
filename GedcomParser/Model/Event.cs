using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace GedcomParser.Model
{
  [DebuggerDisplay("{Type} {Date} {Place}")]
  public class Event : IPrimaryObject
  {
    public Identifiers Id { get; } = new Identifiers();

    public EventType Type { get; set; }
    public string TypeString { get; set; }
    public ExtendedDateRange Date { get; set; }
    public Place Place { get; set; }
    public Organization Organization { get; set; }

    public Dictionary<string, string> Attributes { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public List<Citation> Citations { get; } = new List<Citation>();
    public List<Link> Links { get; } = new List<Link>();
    public List<Media> Media { get; } = new List<Media>();
    public List<Note> Notes { get; } = new List<Note>();

    public void BuildEqualityString(StringBuilder builder, Database db)
    {
      builder.Append(TypeString ?? Type.ToString())
        .Append(Date.ToString("s"))
        .Append(Place?.Names.FirstOrDefault())
        .Append(Organization?.Id.Primary);
      Utilities.BuildEqualityString(this, builder);
    }

    public string GetPreferredId(Database db)
    {
      var builder = new StringBuilder();
      if (Date.HasValue)
        builder.Append(Date.ToString("yyyyMMdd"));
      builder.Append(TypeString ?? Type.ToString());
      if (Place != null)
        Utilities.AddFirstLetters(Place.Names.First().Name, 15, builder);
      return builder.ToString();
    }
  }
}
