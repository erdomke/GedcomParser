using System;
using System.Collections.Generic;
using System.Linq;

namespace GedcomParser.Model
{
  internal class ResolvedEventGroup
  {
    public List<ResolvedEvent> Events { get; }
    public bool Exact { get; }

    public ResolvedEventGroup(IEnumerable<ResolvedEvent> events, bool exact)
    {
      Events = events.ToList();
      Exact = exact;
    }

    public static IList<ResolvedEventGroup> Group(IEnumerable<ResolvedEvent> events, bool exact)
    {
      var result = events
        .Where(e => (e.Event.Type != EventType.Generic
            || e.Event.TypeName == "Met"
            || e.Event.TypeName == "Diagnosis")
          && e.Event.Type != EventType.Census
          && e.Event.Type != EventType.Probate
          && e.Event.Type != EventType.Will
          && !(e.Event.Type == EventType.Residence && e.Event.Place == null))
        .GroupBy(e =>
        {
          if (e.Event.Type == EventType.Birth)
            return "Birth:" 
              + (e.Secondary.Count < 1 ? Guid.NewGuid().ToString("N") : string.Join(":", e.Secondary.Select(s => s.Id.Primary)));
          else if (e.Event.Type == EventType.Adoption)
            return "Adoption:"
              + (e.Secondary.Count < 1 ? Guid.NewGuid().ToString("N") : string.Join(":", e.Secondary.Select(s => s.Id.Primary)));
          else if (e.Event.Type == EventType.Occupation)
            return "Job:" + string.Join(":", e.Primary.Select(s => s.Id.Primary))
              + ":" + (((IHasId)e.Event.Organization ?? e.Event.Place)?.Id.Primary ?? Guid.NewGuid().ToString("N"));
          else if (e.Event.Type == EventType.Degree
            || e.Event.Type == EventType.Graduation)
            return "Degree:" + string.Join(":", e.Primary.Select(s => s.Id.Primary));
          else
            return Guid.NewGuid().ToString("N");
        })
        .Select(g => new ResolvedEventGroup(
          g.OrderBy(e => e.PrimaryOrder).ThenBy(e => e.Event.Date),
          g.Any(e => e.Event.Type == EventType.Birth) ? exact : false
        ))
        .Where(g => g.Events.Any(e => e.Event.Date.HasValue || e.Event.Type == EventType.Birth))
        .OrderBy(g => g.Events.FirstOrDefault(e => e.Event.Date.HasValue)?.Event.Date)
        .ToList();

      var birthGroups = result
        .Where(g => g.Events.Any(e => (e.Event.Type == EventType.Birth || e.Event.Type == EventType.Adoption)
          && e.Secondary.Count > 0))
        .ToList();
      var deathsToChange = result
        .Where(g => g.Events.Any(e => e.Event.Type == EventType.Death))
        .ToList();
      foreach (var death in deathsToChange)
      {
        var people = death.Events.SelectMany(e => e.Primary);
        var birthGroup = birthGroups.FirstOrDefault(g => g.Events.Any(e => e.Primary.Intersect(people).Any()));
        if (birthGroup != null)
        {
          result.Remove(death);
          birthGroup.Events.AddRange(death.Events);
        }
      }

      return result;
    }
  }
}
