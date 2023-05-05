using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace GedcomParser.Model
{
  [DebuggerDisplay("{Name}")]
  public class Individual : IPrimaryObject
  {
    public Identifiers Id { get; } = new Identifiers();

    public Sex Sex { get; set; }
    public PersonName Name => Names.FirstOrDefault()?.Name ?? default;
    public ExtendedDateRange BirthDate => Events.FirstOrDefault(e => e.Type == EventType.Birth)?.Date ?? default;
    public ExtendedDateRange DeathDate => Events.FirstOrDefault(e => e.Type == EventType.Death)?.Date ?? default;
    public List<IndividualName> Names { get; } = new List<IndividualName>();
    public List<Event> Events { get; } = new List<Event>();
    public string Phone { get; set; }
    public string Email { get; set; }
    public string Url { get; set; }

    public List<Citation> Citations { get; } = new List<Citation>();
    public List<Note> Notes { get; } = new List<Note>();
  }
}
