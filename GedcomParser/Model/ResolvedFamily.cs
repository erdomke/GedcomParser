using System;
using System.Collections.Generic;
using System.Linq;

namespace GedcomParser.Model
{
  internal class ResolvedFamily
  {
    public Identifiers Id => Family.Id;
    public Family Family { get; }

    public IEnumerable<Individual> Parents => Members
      .Where(m => m.Role.HasFlag(FamilyLinkType.Parent))
      .Select(m => m.Individual);

    public List<FamilyMember> Members { get; }
    public List<ResolvedEvent> Events { get; }
    public ExtendedDateTime StartDate { get; }

    private ResolvedFamily(Family family, Database db)
    {
      Family = family;
      Members = db.FamilyLinks(family, FamilyLinkType.Other)
        .Select(l => new FamilyMember(db.GetValue<Individual>(l.Individual), l.Type))
        .ToList();
      
      Events = family.Events
        .Select(e => {
          var ev = new ResolvedEvent(e);
          ev.Primary.AddRange(Parents);
          ev.PrimaryRole = FamilyLinkType.Parent;
          return ev;
        })
        .ToList();
      var familyDates = Events
        .Select(e => e.Event)
        .Concat(Members
          .Where(m => m.Role.HasFlag(FamilyLinkType.Child))
          .SelectMany(m => m.Individual.Events))
        .Select(e => e.Date.Start)
        .Where(d => d.HasValue)
        .ToList();
      if (familyDates.Count < 1)
      {
        familyDates.AddRange(Members
          .Where(m => m.Role.HasFlag(FamilyLinkType.Parent))
          .SelectMany(m => m.Individual.Events.Where(e => e.Type == EventType.Birth))
          .Where(e => e.Date.HasValue)
          .Select(e => e.Date.Start.AddYears(16)));
        StartDate = familyDates.Max();
      }
      else
      {
        StartDate = familyDates.Min();
      }
    }

    public IEnumerable<Individual> Children(FamilyLinkType childType = FamilyLinkType.Child)
    {
      return Members
        .Where(m => m.Role.HasFlag(childType))
        .Select(m => m.Individual);
    } 


    public static IEnumerable<ResolvedFamily> Resolve(IEnumerable<Family> families, Database db)
    {
      var result = families
        .Select(f => new ResolvedFamily(f, db))
        .ToList();
      var individualLookup = result.SelectMany(f => f.Members
        .Select(i => ValueTuple.Create(i, f)))
        .ToLookup(t => t.Item1.Individual, t => ValueTuple.Create(t.Item1.Role, t.Item2));
      foreach (var familyList in individualLookup)
      {
        var individualFamilies = familyList.OrderBy(f => f.Item2.StartDate).ToList();
        var ranges = new List<ExtendedDateRange>();
        for (var i = 0; i < individualFamilies.Count; i++)
        {
          var start = individualFamilies[i].Item2.StartDate;
          if (i == 0)
            start = default(ExtendedDateTime);
          var end = default(ExtendedDateTime);
          if (i + 1 < individualFamilies.Count)
            end = individualFamilies[i + 1].Item2.StartDate;
          ranges.Add(new ExtendedDateRange(start, end));
        }

        var individualEvents = new List<ResolvedEvent>();
        foreach (var individualEvent in familyList.Key.Events)
        {
          if (individualEvent.Type == EventType.Burial)
          {
            var deathEvent = individualEvents.FirstOrDefault(e => e.Event.Type == EventType.Death);
            if (deathEvent != null)
            {
              deathEvent.Related.Add(individualEvent);
              continue;
            }
          }
          individualEvents.Add(new ResolvedEvent(individualEvent));
        }

        foreach (var resolved in individualEvents)
        {
          var idx = ranges.FindIndex(r => r.InRange(resolved.Event.Date));
          if (idx >= 0)
          {
            resolved.Primary.Add(familyList.Key);
            resolved.PrimaryRole = individualFamilies[idx].Item1;
            if (individualFamilies[idx].Item1.HasFlag(FamilyLinkType.Child)
              && (resolved.Event.Type == EventType.Birth
                || resolved.Event.Type == EventType.Adoption))
              resolved.Secondary.AddRange(individualFamilies[idx].Item2.Parents);
            individualFamilies[idx].Item2.Events.Add(resolved);
          }
        }
      }
      return result;
    }
  }

  public class IndividualEvent
  {
    public Individual Individual { get; }
    public Event Event { get; }

    public IndividualEvent(Individual individual, Event @event)
    {
      Individual = individual;
      Event = @event;
    }
  }
}
