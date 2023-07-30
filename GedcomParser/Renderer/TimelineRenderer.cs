using GedcomParser.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml.Linq;

namespace GedcomParser.Renderer
{
  internal class TimelineRenderer
  {
    public IGraphics Graphics { get; set; }

    internal static List<ResolvedEvent> AllEvents(IEnumerable<ResolvedFamily> families)
    {
      var events = families
        .SelectMany(f => f.Events)
        .Where(e => e.Event.Date.HasValue
          && (e.Event.Type == EventType.Birth
            || e.Event.Type == EventType.Death
            || e.Event.Type == EventType.Marriage
            || e.Event.Type == EventType.Adoption))
        .ToList();
      events.AddRange(families
        .SelectMany(f => f.Members)
        .Select(m => m.Individual)
        .Distinct()
        .SelectMany(i => i.Events
          .Where(e => e.Date.HasValue
            && (e.Type == EventType.Birth || e.Type == EventType.Death))
          .Select(e =>
          {
            var resolved = new ResolvedEvent(e);
            resolved.Primary.Add(i);
            return resolved;
          }))
        .Where(n => !events.Any(e => e.Event.Type == n.Event.Type && e.Primary.Contains(n.Primary[0]))));
      events.Sort((x, y) => x.Event.Date.CompareTo(y.Event.Date));
      return events;
    }

    public XElement Render(IEnumerable<ResolvedFamily> families, string baseDirectory, HashSet<string> directAncestors)
    {
      var lines = families
        .SelectMany(f => f.Members)
        .Where(m => !m.Role.HasFlag(FamilyLinkType.Child) || DescendantLayout.IncludeChild(families, m.Individual, directAncestors))
        .Select(m => m.Individual)
        .Where(i => i.BirthDate.HasValue || i.DeathDate.HasValue)
        .Distinct()
        .Select(i => new IndividualTimeline(i))
        .ToDictionary(i => i.Individual.Id.Primary);
      if (lines.Count <= 1)
        return null;
      var events = AllEvents(families);

      var links = new List<EventLink>();
      foreach (var resolvedEvent in events)
      {
        foreach (var line in resolvedEvent.Primary
            .Concat(resolvedEvent.Secondary)
            .Select(i => lines.TryGetValue(i.Id.Primary, out var line) ? line : null)
            .Where(l => l != null))
        {
          line.AddEvent(resolvedEvent);
        }
        links.Add(new EventLink(resolvedEvent, lines));
      }

      var startDate = lines.Values.Where(l => l.Start.HasValue).Min(l => l.Start);
      var endDate = lines.Values.Where(l => l.End.HasValue).Max(l => l.End);

      var result = new XElement(SvgUtil.Ns + "svg");
      if (!startDate.HasValue || !endDate.HasValue)
        return result;

      startDate = new DateTime(startDate.Value.Year - (startDate.Value.Year % 10), 1, 1);
      if (endDate <= DateTime.Now)
        endDate = new DateTime(endDate.Value.Year - (startDate.Value.Year % 10) + 10, 1, 1);
      var pxPerDay = 6 * 96.0 / (endDate.Value - startDate.Value).TotalDays;

      var grid = new XElement(SvgUtil.Ns + "g");
      result.Add(grid);
      
      var top = 0.0;
      var remaining = lines.Values
        .Where(l => l.Start.HasValue)
        .OrderBy(l => l.Start.Value)
        .ToList();
      var orderedLines = new List<IndividualTimeline>();
      while (remaining.Count > 0)
      {
        orderedLines.Add(Remove(remaining, remaining[0]));
        for (var i = orderedLines.Count - 1; i < orderedLines.Count; i++)
        {
          var insertAt = i;
          foreach (var link in links.Where(l => l.Date.HasValue
            && l.Nodes.Any(n => n.Item1 == orderedLines[i]))
            .OrderBy(l => l.Date.Value))
          {
            foreach (var line in link.Nodes
              .Select(n => n.Item1)
              .Where(l => remaining.Contains(l))
              .Distinct()
              .OrderBy(l => l.Start)
              .ToList())
            {
              insertAt++;
              orderedLines.Insert(insertAt, Remove(remaining, line));
            }
          }
        }
      }

      foreach (var line in orderedLines)
      {
        line.Top = top;
        line.Height = 24;
        line.SetPosition(startDate.Value, pxPerDay, Graphics);
        top += line.Height + 4;
        result.Add(line.ToSvg());
      }
      foreach (var eventLink in links
        .Where(l => l.Date.HasValue)
        .OrderBy(l => l.Date))
      {
        eventLink.DateX = (eventLink.Date.Value - startDate.Value).TotalDays * pxPerDay;
        result.Add(eventLink.ToSvg());
      }

      var height = top;

      var interval = 10;
      var lineCount = (endDate.Value.Year - startDate.Value.Year) / interval;
      while (lineCount > 16)
      {
        interval += 10;
        lineCount = (endDate.Value.Year - startDate.Value.Year) / interval;
      }
      var style = ReportStyle.Default;
      for (var i = 0; i <= lineCount; i++)
      {
        var decadeStart = startDate.Value.AddYears(interval * i);
        var position = (decadeStart - startDate.Value).TotalDays * pxPerDay;
        grid.Add(new XElement(SvgUtil.Ns + "line"
          , new XAttribute("x1", position)
          , new XAttribute("y1", -14)
          , new XAttribute("x2", position)
          , new XAttribute("y2", height)
          , new XAttribute("style", $"stroke-width: 1px; stroke: black; opacity:0.1;")));
        if (i < lineCount)
        {
          grid.Add(new XElement(SvgUtil.Ns + "text"
            , new XAttribute("x", position + 1)
            , new XAttribute("y", -6)
            , new XAttribute("style", $"fill:#ccc;font-size:{style.BaseFontSize - 2}px;font-family:{style.FontName}")
            , decadeStart.Year));
        }
      }

      var topPosition = -20;
      height -= topPosition;
      var left = lines.Values.Min(l => l.Left);
      var right = lines.Values.Max(l => l.Right);
      var width = right - left;
      result.SetAttributeValue("viewBox", $"{left} {topPosition} {width} {height}");
      result.SetAttributeValue("style", $"width:{width}px;height:{height};");
      return result;
    }

    private T Remove<T>(List<T> remaining, T value)
    {
      var idx = remaining.IndexOf(value);
      if (idx >= 0)
        remaining.RemoveAt(idx);
      return value;
    }

    private class EventLink : Shape
    {
      public List<(IndividualTimeline, bool)> Nodes { get; } = new List<(IndividualTimeline, bool)>();

      public DateTime? Date { get; }

      public double DateX { get; set; }

      public EventType Type { get; }

      public EventLink(ResolvedEvent resolvedEvent, Dictionary<string, IndividualTimeline> lines)
      {
        Date = GetDate(resolvedEvent.Event.Date);
        Type = resolvedEvent.Event.Type;
        foreach (var timeline in resolvedEvent.Primary
          .Select(i => lines.TryGetValue(i.Id.Primary, out var line) ? line : null)
          .Where(l => l != null))
          Nodes.Add((timeline, true));
        foreach (var timeline in resolvedEvent.Secondary
          .Select(i => lines.TryGetValue(i.Id.Primary, out var line) ? line : null)
          .Where(l => l != null))
          Nodes.Add((timeline, false));
      }

      public override IEnumerable<XElement> ToSvg()
      {
        var nodes = Nodes
          .Where(n => n.Item1.Start.HasValue)
          .OrderBy(n => n.Item1.Top)
          .ToList();
        var group = new XElement(SvgUtil.Ns + "g");
        if (nodes.Count > 1)
        {
          group.Add(new XElement(SvgUtil.Ns + "line"
            , new XAttribute("x1", DateX)
            , new XAttribute("y1", nodes[0].Item1.Bottom - 5)
            , new XAttribute("x2", DateX)
            , new XAttribute("y2", nodes.Last().Item1.Bottom - 5)
            , new XAttribute("style", $"stroke-width: 1px; stroke: black; opacity:0.4;")));
        }
        foreach (var node in nodes)
        {
          group.Add(new XElement(SvgUtil.Ns + "circle"
            , new XAttribute("cx", DateX)
            , new XAttribute("cy", node.Item1.Bottom - 5)
            , new XAttribute("r", 5)
            , new XAttribute("style", $"stroke-width: 2px; stroke: black; fill: {(node.Item2 ? "black" : "white")}")));
        }
        yield return group;
      }

      private DateTime? GetDate(ExtendedDateRange range)
      {
        if (!range.TryGetRange(out var start, out var end))
          return null;
        if (start.HasValue && end.HasValue)
          return (start.Value + (end.Value - start.Value) / 2).Date;
        else
          return (start ?? end).Value.Date;
      }
    }

    private class IndividualTimeline : Shape
    {
      private XElement _group;

      public Individual Individual { get; }

      public DateTime? Start { get; private set; }

      public DateTime? End { get; private set; }

      public string Name { get; }
      public string BirthDate { get; }
      public string DeathDate { get; }

      public IndividualTimeline(Individual individual) 
      {
        _group = new XElement(SvgUtil.Ns + "g", new XAttribute("id", "time-" + individual.Id.Primary));
        Individual = individual;
        var deathFound = individual.Events.Any(e => e.Type == EventType.Death
          || e.Type == EventType.Burial);

        Name = individual.Name.Name.ToString();
        BirthDate = individual.BirthDate.ToString("s");
        if (deathFound)
        {
          if (individual.DeathDate.HasValue)
            DeathDate = individual.DeathDate.ToString("s");
          else
            DeathDate = "Deceased";
        }

        if (!deathFound && (!Start.HasValue || (DateTime.Now.Year - Start.Value.Year) < 120))
          End = new DateTime(DateTime.Now.Year - (DateTime.Now.Year % 10) + 10, 1, 1);
      }

      public void AddEvent(ResolvedEvent eventObj)
      {
        if (eventObj.Event.Date.TryGetRange(out var start, out var end))
        {
          var earliest = start ?? end;
          var latest = end ?? start;

          if (!Start.HasValue || earliest < Start)
          {
            if ((eventObj.Primary.Contains(Individual) && eventObj.Event.Type == EventType.Marriage)
              || (eventObj.Secondary.Contains(Individual) 
                && (eventObj.Event.Type == EventType.Birth || eventObj.Event.Type == EventType.Adoption)))
              earliest = earliest.Value.AddYears(-14);
          }

          if (!Start.HasValue || earliest < Start)
            Start = earliest;
          if (!End.HasValue || latest > End)
            End = latest;
        }
      }

      public void SetPosition(DateTime startDate, double pxPerDay, IGraphics graphics)
      {
        var style = ReportStyle.Default;
        style.BaseFontSize = 14;
        var rectLeft = (Start.Value - startDate).TotalDays * pxPerDay;
        var rectWidth = (End.Value - Start.Value).TotalDays * pxPerDay;
        _group.Add(new XElement(SvgUtil.Ns + "rect"
          , new XAttribute("x", rectLeft)
          , new XAttribute("y", Bottom - 10)
          , new XAttribute("width", rectWidth)
          , new XAttribute("height", 10)
          , new XAttribute("style", "fill:#ccc;")));
        var nameSize = graphics.MeasureText(style.FontName, style.BaseFontSize, Name);
        var dateFontSize = style.BaseFontSize - 2;
        var birthDateSize = graphics.MeasureText(style.FontName, dateFontSize, BirthDate);
        var deathDateSize = graphics.MeasureText(style.FontName, dateFontSize, DeathDate);

        Left = rectLeft - (birthDateSize.Width + 10);
        Width = Math.Max(nameSize.Width, birthDateSize.Width + 10 + rectWidth + 10 + deathDateSize.Width);

        _group.Add(new XElement(SvgUtil.Ns + "text"
          , new XAttribute("x", Left)
          , new XAttribute("y", Bottom - 13)
          , new XAttribute("style", $"font-size:{style.BaseFontSize}px;font-family:{style.FontName}")
          , Name));

        _group.Add(new XElement(SvgUtil.Ns + "text"
          , new XAttribute("x", Left)
          , new XAttribute("y", Bottom - 1)
          , new XAttribute("style", $"fill:#999;font-size:{dateFontSize}px;font-family:{style.FontName}")
          , BirthDate));

        _group.Add(new XElement(SvgUtil.Ns + "text"
          , new XAttribute("x", Right - deathDateSize.Width)
          , new XAttribute("y", Bottom - 1)
          , new XAttribute("style", $"fill:#999;font-size:{dateFontSize}px;font-family:{style.FontName}")
          , DeathDate));
      }

      public override IEnumerable<XElement> ToSvg()
      {
        yield return _group;
      }
    }
  }
}
