using System.Collections.Generic;

namespace GedcomParser.Model
{
  public class Family : IPrimaryObject
  {
    public Identifiers Id { get; } = new Identifiers();
    public List<Event> Events { get; } = new List<Event>();
    public FamilyType Type { get; set; }

    public Dictionary<string, string> Attributes { get; } = new Dictionary<string, string>();
    public List<Citation> Citations { get; } = new List<Citation>();
    public List<Link> Links { get; } = new List<Link>();
    public List<Media> Media { get; } = new List<Media>();
    public List<Note> Notes { get; } = new List<Note>();
  }
}
