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
    public List<Media> Media { get; } = new List<Media>();

    private ResolvedFamily(Family family, Database db) 
    {
      Family = family;
      Media.AddRange(family.Media);
      Members = db.FamilyLinks(family, FamilyLinkType.Other)
        .OrderBy(l => l.Order)
        .Select(l => new FamilyMember(db.GetValue<Individual>(l.Individual), l.Type, l.Order))
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

      if (familyDates.Count >= 1) 
      {
        StartDate = familyDates.Min();
        return;
      }

      familyDates.AddRange(Members
        .Where(m => m.Role.HasFlag(FamilyLinkType.Parent))
        .SelectMany(m => m.Individual.Events.Where(e => e.Type == EventType.Birth))
        .Where(e => e.Date.HasValue)
        .Select(e => e.Date.Start.AddYears(16)));
      if (familyDates.Count > 0)
      {
        StartDate = familyDates.Max();
        return;
      }

      foreach (var parent in Parents.Where(p => !p.FamilyInferredBirthDate.HasValue))
      {
        InferBirthDates(db.Siblings(parent));
      }

      familyDates.AddRange(Members
        .Where(m => m.Role.HasFlag(FamilyLinkType.Parent)
          && m.Individual.FamilyInferredBirthDate.HasValue)
        .Select(m => m.Individual.FamilyInferredBirthDate.Start.AddYears(16)));
      if (familyDates.Count > 0)
      {
        StartDate = familyDates.Max();
        return;
      }
    }

    private void InferBirthDates(IEnumerable<Individual> individuals)
    {
      var siblings = individuals.ToList();
      if (!siblings.Any(i => i.BirthDate.HasValue))
        return;
      var start = 0;
      while (true)
      {
        var end = siblings.FindIndex(start, i => i.BirthDate.HasValue);
        if (start == end)
        {
          start++;
          continue;
        }
        else if (end < 0)
        {
          if (start < siblings.Count)
          {
            var date = siblings[start - 1].BirthDate;
            for (var i = start; i < siblings.Count; i++)
            {
              date = new ExtendedDateRange(date.Start.AddYears(2));
              siblings[i].FamilyInferredBirthDate = date;
            }
          }
          break;
        }
        else if (start == 0)
        {
          var date = siblings[end].BirthDate;
          for (var i = end - 1; i >= 0; i--)
          {
            date = new ExtendedDateRange(date.Start.AddYears(-2));
            siblings[i].FamilyInferredBirthDate = date;
          }
          start = end + 1;
        }
        else if (siblings[start - 1].BirthDate.TryGetDiff(siblings[end].BirthDate, out var min, out var _))
        {
          var months = (int)(min.TotalMonths / (end - start + 1));
          var date = siblings[start - 1].BirthDate;
          for (var i = start; i < end; i++)
          {
            date = new ExtendedDateRange(date.Start.AddMonths(months));
            siblings[i].FamilyInferredBirthDate = date;
          }
          start = end + 1;
        }
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
        .Select(i => new { Member = i, Family = f }))
        .OrderBy(t => t.Member.Order)
        .ToLookup(t => t.Member.Individual, t => new MemberFamily(t.Family, t.Member.Role, t.Member.Order));
      foreach (var memberFamilies in individualLookup)
      {
        var individual = memberFamilies.Key;
        var individualFamilies = memberFamilies.OrderBy(f => f.Family.StartDate).ToList();
        var ranges = new List<ExtendedDateRange>();
        for (var i = 0; i < individualFamilies.Count; i++)
        {
          var start = individualFamilies[i].Family.StartDate;
          if (i == 0)
            start = default(ExtendedDateTime);
          var end = default(ExtendedDateTime);
          if (i + 1 < individualFamilies.Count)
            end = individualFamilies[i + 1].Family.StartDate;
          ranges.Add(new ExtendedDateRange(start, end));
        }

        var individualEvents = individual.Events.Select(e => new ResolvedEvent(e)).ToList();
        foreach (var ev in individualEvents.Where(e => e.Event.Type == EventType.Burial
          || (string.Equals(e.Event.TypeString, "Diagnosis", StringComparison.OrdinalIgnoreCase) && !e.Event.Date.HasValue))
          .ToList())
        {
          var deathEvent = individualEvents.FirstOrDefault(e => e.Event.Type == EventType.Death);
          if (deathEvent != null)
          {
            deathEvent.Related.Add(ev.Event);
            individualEvents.Remove(ev);
          }
        }

        if (!individual.Events.Any(e => e.Type == EventType.Birth))
        {
          var memberFamily = individualFamilies
            .FirstOrDefault(f => f.Role.HasFlag(FamilyLinkType.Birth));
          if (memberFamily != null)
          {
            var resolved = new ResolvedEvent(new Event()
            {
              Type = EventType.Birth
            });
            resolved.Primary.Add(individual);
            resolved.PrimaryOrder = memberFamily.Order;
            resolved.PrimaryRole = FamilyLinkType.Birth;
            resolved.Secondary.AddRange(memberFamily.Family.Parents);
            memberFamily.Family.Events.Add(resolved);
          }
        }

        foreach (var resolved in individualEvents)
        {
          var familyLink = default(MemberFamily);
          if (resolved.Event.Type == EventType.Birth)
          {
            familyLink = individualFamilies
              .FirstOrDefault(f => f.Role.HasFlag(FamilyLinkType.Birth));
          }
          else if (resolved.Event.Type == EventType.Adoption)
          {
            familyLink = individualFamilies
              .FirstOrDefault(f => f.Role.HasFlag(FamilyLinkType.Pet) || f.Role.HasFlag(FamilyLinkType.Adopted));
          }

          if (familyLink == null)
          {
            var idx = ranges.FindIndex(r => r.InRange(resolved.Event.Date));
            if (idx >= 0)
              familyLink = individualFamilies[idx];
          }
          else
          {
            resolved.Secondary.AddRange(familyLink.Family.Parents);
          }

          if (familyLink != null)
          {
            resolved.Primary.Add(memberFamilies.Key);
            resolved.PrimaryOrder = familyLink.Order;
            resolved.PrimaryRole = familyLink.Role;
            familyLink.Family.Events.Add(resolved);
          }
        }

        foreach (var media in individual.Media)
        {
          var date = media.TopicDate.HasValue ? media.TopicDate : media.Date;
          if (!date.HasValue)
            continue;

          var idx = ranges.FindIndex(r => r.InRange(date));
          if (idx < 0)
            continue;

          individualFamilies[idx].Family.Media.Add(media);
        }
      }
      return result;
    }

    private class MemberFamily
    {
      public ResolvedFamily Family { get; }
      public FamilyLinkType Role { get; }
      public int Order { get; }

      public MemberFamily(ResolvedFamily family, FamilyLinkType role, int order)
      {
        Family = family;
        Role = role;
        Order = order;
      }
    }
  }
}
