using System.Collections.Generic;
using System.Diagnostics;

namespace GedcomParser.Model
{
  [DebuggerDisplay("{Event.DebuggerDisplay,nq}")]
  internal class ResolvedEvent
  {
    public List<Individual> Primary { get; } = new List<Individual>();

    public int PrimaryOrder { get; set; }

    public FamilyLinkType PrimaryRole { get; set; }

    public List<Individual> Secondary { get; } = new List<Individual>();

    public Event Event { get; }

    public List<Event> Related { get; } = new List<Event>();

    public ResolvedEvent(Event eventObj)
    {
      Event = eventObj;
    }
  }
}
