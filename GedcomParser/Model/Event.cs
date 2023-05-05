using System.Collections.Generic;
using System.Diagnostics;

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
    public List<Citation> Citations { get; } = new List<Citation>();
    public List<Note> Notes { get; } = new List<Note>();
  }
}
