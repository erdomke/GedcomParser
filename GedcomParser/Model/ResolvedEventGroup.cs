using System;
using System.Collections.Generic;
using System.Linq;

namespace GedcomParser.Model
{
  internal class ResolvedEventGroup
  {
    public List<ResolvedEvent> Events { get; }

    public ResolvedEventGroup(IEnumerable<ResolvedEvent> events)
    {
      Events = events.ToList();
    }

    public void Description(HtmlTextWriter html, SourceListSection sourceList, bool includeDate)
    {
      if (Events.Any(e => e.Event.Type == EventType.Birth && e.Secondary.Count > 0))
      {
        var births = Events
          .Where(e => e.Event.Type == EventType.Birth)
          //.OrderBy(e => e.Event.Date)
          .ToList();
        var deaths = Events
          .Where(e => e.Event.Type == EventType.Death)
          .ToDictionary(e => e.Primary.First().Id.Primary);
        
        ResolvedEvent.AddNames(html, births.First().Secondary, ResolvedEvent.NameForm.Short, births.Count > 1 ? default : births.First().Event.Date);
        html.WriteString(" gave birth to ");
        if (births.Count == 1)
          html.WriteString("1 child");
        else
          html.WriteString(births.Count + " children");
        html.WriteString(" -- ");
        var first = true;
        foreach (var birth in births)
        {
          if (first)
            first = false;
          else
            html.WriteString("; ");
          if (birth.Primary.First().Sex == Sex.Male)
            html.WriteString("a boy ");
          else if (birth.Primary.First().Sex == Sex.Female)
            html.WriteString("a girl ");
          ResolvedEvent.AddNames(html, birth.Primary, ResolvedEvent.NameForm.All, default);
          ResolvedEvent.AddDate(html, birth.Event.Date, true);
          if (deaths.TryGetValue(birth.Primary.First().Id.Primary, out var deathEvent))
          {
            if (deathEvent.Event.Date.HasValue)
              html.WriteString(" (died " + deathEvent.Event.Date.ToString(ResolvedEvent.DateFormat) + ")");
            else
              html.WriteString(" (deceased)");
          }
        }
        html.WriteString(".");
      }
      else if (Events.Any(e => e.Event.Type == EventType.Occupation))
      {
        ResolvedEvent.AddNames(html, Events[0].Primary, ResolvedEvent.NameForm.Short, default);
        html.WriteString(" worked");
        ResolvedEvent.AddPlace(html, Events[0].Event);
        var first = true;
        foreach (var ev in Events)
        {
          if (first)
            first = false;
          else
            html.WriteString(", ");
          if (ev.Event.Attributes.TryGetValue("Occupation", out var occupation))
          {
            html.WriteString(" as a ");
            html.WriteString(occupation);
          }
          ResolvedEvent.AddDate(html, ev.Event.Date, includeDate);
        }
        html.WriteString(".");
      }
      else if (Events.Any(e => e.Event.Type == EventType.Degree
        || e.Event.Type == EventType.Graduation))
      {
        ResolvedEvent.AddNames(html, Events[0].Primary, ResolvedEvent.NameForm.Short, default);
        html.WriteString(" graduated");
        var first = true;
        foreach (var ev in Events)
        {
          if (first)
            first = false;
          else
            html.WriteString(", ");

          if (ev.Event.Attributes.TryGetValue("Degree", out var degree))
          {
            html.WriteString(" with a ");
            html.WriteString(degree);
          }
          ResolvedEvent.AddPlace(html, ev.Event, "from");
          ResolvedEvent.AddDate(html, ev.Event.Date, includeDate, ev.Primary[0]);
        }
        html.WriteString(".");
      }
      else
      {
        var first = true;
        foreach (var ev in Events)
        {
          if (first)
            first = false;
          else
            html.WriteString(" ");
          ev.Description(html, includeDate);
        }
      }

      var eventCitations = Events.SelectMany(e => e.Event.Citations)
        .Select(c => new { Id = c.Id.Primary, Index = sourceList.Citations.IndexOf(c) })
        .Where(c => c.Index >= 0)
        .GroupBy(c => c.Index)
        .Select(g => g.First())
        .OrderBy(c => c.Index)
        .ToList();
      if (eventCitations.Count > 0)
      {
        html.WriteString(" ");
        html.WriteStartElement("sup");
        html.WriteAttributeString("class", "cite");
        html.WriteString("[");
        var first = true;
        foreach (var citation in eventCitations)
        {
          if (first)
            first = false;
          else
            html.WriteString(", ");
          html.WriteStartElement("a");
          html.WriteAttributeString("href", "#" + citation.Id);
          html.WriteString((citation.Index + 1).ToString());
          html.WriteEndElement();
        }
        html.WriteString("]");
        html.WriteEndElement();
      }
    }


    public static IList<ResolvedEventGroup> Group(IEnumerable<ResolvedEvent> events)
    {
      var result = events
        .Where(e => e.Event.Date.HasValue)
        .OrderBy(e => e.Event.Date)
        .GroupBy(e =>
        {
          if (e.Event.Type == EventType.Birth)
            return "Birth:" 
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
        .Select(g => new ResolvedEventGroup(g))
        .OrderBy(g => g.Events.First().Event.Date)
        .ToList();

      var birthGroups = result
        .Where(g => g.Events.Any(e => e.Event.Type == EventType.Birth && e.Secondary.Count > 0))
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
