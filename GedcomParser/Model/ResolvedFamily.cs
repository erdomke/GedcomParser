using System;
using System.Collections.Generic;
using System.Linq;

namespace GedcomParser.Model
{
  internal class ResolvedFamily
  {
    public Identifiers Id { get; }
    public List<Individual> Parents { get; }
    public List<Individual> Children { get; }
    public List<Event> Events { get; }
    public ExtendedDateTime StartDate { get; }

    private ResolvedFamily(Family family, Database db)
    {
      Id = family.Id;
      Parents = db.FamilyLinks(family, FamilyLinkType.Parent)
        .Select(l => db.GetValue<Individual>(l.Individual))
        .ToList();
      Children = db.FamilyLinks(family, FamilyLinkType.Child)
        .Select(l => db.GetValue<Individual>(l.Individual))
        .ToList();
      
      Events = family.Events.ToList();
      var familyDates = Events
        .Concat(Children.SelectMany(c => c.Events))
        .Select(e => e.Date.Start)
        .Where(d => d.HasValue)
        .ToList();
      if (familyDates.Count < 1)
      {
        familyDates.AddRange(Parents
          .SelectMany(p => p.Events.Where(e => e.Type == EventType.Birth))
          .Where(e => e.Date.HasValue)
          .Select(e => e.Date.Start.AddYears(16)));
        StartDate = familyDates.Max();
      }
      else
      {
        StartDate = familyDates.Min();
      }
    }

    public static IEnumerable<ResolvedFamily> Resolve(IEnumerable<Family> families, Database db)
    {
      var result = families
        .Select(f => new ResolvedFamily(f, db))
        .ToList();
      var individualLookup = result.SelectMany(f => f.Parents
        .Concat(f.Children)
        .Select(i => ValueTuple.Create(i, f)))
        .ToLookup(t => t.Item1, t => t.Item2);
      foreach (var familyList in individualLookup)
      {
        var individualFamilies = familyList.OrderBy(f => f.StartDate).ToList();
        var ranges = new List<ExtendedDateRange>();
        for (var i = 0; i < individualFamilies.Count; i++)
        {
          var start = individualFamilies[i].StartDate;
          if (i == 0)
            start = default(ExtendedDateTime);
          var end = default(ExtendedDateTime);
          if (i + 1 < individualFamilies.Count)
            end = individualFamilies[i + 1].StartDate;
          ranges.Add(new ExtendedDateRange(start, end));
        }
        foreach (var individualEvent in familyList.Key.Events)
        {
          var idx = ranges.FindIndex(r => r.InRange(individualEvent.Date));
          if (idx >= 0)
            individualFamilies[idx].Events.Add(individualEvent);
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
