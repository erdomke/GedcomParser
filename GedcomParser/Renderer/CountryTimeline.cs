using GedcomParser.Model;
using Microsoft.Msagl.Core.Layout;
using Svg;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;

namespace GedcomParser
{
  internal class CountryTimeline
  {
    private readonly Database _database;
    private readonly IEnumerable<ResolvedFamily> _resolvedFamilies;

    public List<Legend> LegendEntries { get; } = new List<Legend>();

    public CountryTimeline(Database database, IEnumerable<ResolvedFamily> resolvedFamilies)
    {
      _database = database;
      _resolvedFamilies = resolvedFamilies;
    }

    public XElement Render(string root)
    {
      var diagram = new Diagram()
      {
        Width = 7 * 96,
        Height = 4 * 96,
        LeftDate = new DateTime(DateTime.Now.Year - (DateTime.Now.Year % 10) + 10, 1, 1),
      };
      var segment = CreateSegment(root, DateTime.Now, diagram);
      var percentages = new Dictionary<string, double>();
      segment.CalculatePercentages(1, percentages);
      var colors = diagram.PlaceColors();
      LegendEntries.AddRange(percentages.Keys
        .Union(colors.Keys)
        .Select(p => new Legend(p
          , colors.TryGetValue(p, out var color) ? color : "white"
          , percentages.TryGetValue(p, out var percentage) ? percentage : 0))
        .OrderByDescending(l => Math.Round(l.Percentage, 3))
        .ThenBy(l => l.Name, StringComparer.OrdinalIgnoreCase));
      diagram.RightDate = new DateTime(diagram.RightDate.Year - (diagram.RightDate.Year % 10), 1, 1);

      var style = ReportStyle.Default;
      var height = style.BaseFontSize + diagram.Height;
      var result = new XElement(SvgUtil.Ns + "svg");
      result.SetAttributeValue("viewBox", $"0 0 {diagram.Width} {height}");
      result.SetAttributeValue("width", diagram.Width);
      result.SetAttributeValue("height", height);
      segment.AddInformation(result, diagram, 0, diagram.Height);
      var century = new DateTime(diagram.LeftDate.Year - (diagram.LeftDate.Year % 100), 1, 1);
      while (century > diagram.RightDate)
      {
        var location = diagram.DateLocation(century);
        result.Add(new XElement(SvgUtil.Ns + "line"
          , new XAttribute("x1", location)
          , new XAttribute("y1", 0)
          , new XAttribute("x2", location)
          , new XAttribute("y2", height)
          , new XAttribute("style", "stroke-width:1px;stroke:black")
        ));

        result.Add(new XElement(SvgUtil.Ns + "text"
          , new XAttribute("x", location + 2)
          , new XAttribute("y", height - 3)
          , new XAttribute("style", $"font-size:{style.BaseFontSize}px;font-family:{style.FontName}")
          , century.Year));
        century = century.AddYears(-100);
      }

      return result;
    }

    public record Legend(string Name, string Color, double Percentage);

    private bool ValidEventType(ResolvedEvent resolvedEvent, string personId)
    {
      if (resolvedEvent.Primary.Concat(resolvedEvent.Secondary).Any(i => i.Id.Contains(personId)))
        return resolvedEvent.Event.Type == EventType.Birth
          || resolvedEvent.Event.Type == EventType.Immigration
          || resolvedEvent.Event.Type == EventType.Residence
          || resolvedEvent.Event.Type == EventType.Census
          || resolvedEvent.Event.Type == EventType.Death;
      else if (resolvedEvent.PrimaryRole.HasFlag(FamilyLinkType.Parent))
        return resolvedEvent.Event.Type == EventType.Birth
          || resolvedEvent.Event.Type == EventType.Immigration
          || resolvedEvent.Event.Type == EventType.Residence
          || resolvedEvent.Event.Type == EventType.Census;
      else
        return false;
    }

    private Segment CreateSegment(string personId, DateTime endDate, Diagram diagram)
    {
      var segment = new Segment()
      {
        Individual = _database.GetValue<Individual>(personId),
      };
      if (!segment.Individual.BirthDate.TryGetRange(out var birthDate, out var _))
        birthDate = DateTime.MinValue;

      var events = _resolvedFamilies
        .Where(f => f.Members.Any(m => m.Individual.Id.Contains(personId)))
        .SelectMany(f => f.Events)
        .Concat(segment.Individual.Events
          .Where(e => e.Type == EventType.Residence || e.Type == EventType.Immigration)
          .Select(e =>
          {
            var ev = new ResolvedEvent(e);
            ev.Primary.Add(segment.Individual);
            return ev;
          })
        ).Where(e => ValidEventType(e, personId)
          && e.Event.Date.HasValue
          && TryGetKey(e.Event.Place, out var _))
        .OrderByDescending(e => e.Event.Date)
        .ToList();
      foreach (var ev in events)
      {
        if (ev.Event.Date.TryGetRange(out var start, out var end))
        {
          if (start.HasValue
            && start.Value < endDate
            && start.Value >= birthDate
            && TryGetKey(ev.Event.Place, out var placeName))
          {
            if (segment.Places.Count > 0 && segment.Places.Last().PlaceName == placeName)
            {
              segment.Places.Last().Start = start.Value;
            }
            else
            {
              segment.Places.Add(new PlaceRange()
              {
                Start = start.Value,
                End = endDate,
                PlaceName = placeName
              });
            }
            diagram.AddPlace(segment.Places.Last());
            endDate = start.Value;
          }
        }
      }
      
      var birthFamily = _resolvedFamilies
        .FirstOrDefault(f => f.Members.Any(m => m.Individual.Id.Contains(personId) && m.Role.HasFlag(FamilyLinkType.Birth)));
      if (birthFamily != null)
      {
        foreach (var parent in birthFamily.Parents)
          segment.Ancestors.Add(CreateSegment(parent.Id.Primary, endDate, diagram));
      }

      return segment;
    }

    private bool TryGetKey(Place place, out string key)
    {
      var partDict = (place?.Names.FirstOrDefault(n => n.Parts.Count > 0)?.Parts
        ?? Enumerable.Empty<KeyValuePair<string, string>>())
        .ToDictionary(k => k.Key, k => k.Value);
      var keyParts = new List<string>();
      //if (partDict.TryGetValue("state", out var state))
      //  keyParts.Add(state);
      if (partDict.TryGetValue("country", out var country))
        keyParts.Add(country);

      key = string.Join(", ", keyParts);
      return keyParts.Count > 0;
    }

    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    private class PlaceRange
    {
      public DateTime Start { get; set; }
      public DateTime End { get; set; }
      public string PlaceName { get; set; }

      private string DebuggerDisplay => $"{Start:yyyy-MM-dd}/{End:yyyy-MM-dd} at {PlaceName}";
    }

    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    private class Segment
    {
      public Individual Individual { get; set; }
      public List<PlaceRange> Places { get; } = new List<PlaceRange>();
      public List<Segment> Ancestors { get; } = new List<Segment>();

      private string DebuggerDisplay => $"{Places.Min(p => p.Start):yyyy-MM-dd}/{Places.Max(p => p.End):yyyy-MM-dd} for {Individual.Name.Name}";

      public void CalculatePercentages(double percent, Dictionary<string, double> places)
      {
        var toInclude = Ancestors
          .Where(a => a.Places.Count > 0 || a.Ancestors.Any(g => g.Places.Count > 0))
          .ToList();
        if (toInclude.Count < 2)
        {
          var placeName = Places.LastOrDefault()?.PlaceName ?? "Unknown";
          if (!places.TryGetValue(placeName, out var currentPercentage))
            currentPercentage = 0;
          places[placeName] = currentPercentage + percent / (toInclude.Count == 1 ? 2 : 1);
        }

        var count = Math.Max(toInclude.Count, 2);
        foreach (var ancestor in toInclude)
        {
          ancestor.CalculatePercentages(percent / count, places);
        }
      }

      public void AddInformation(XElement svg, Diagram diagram, double top, double height)
      {
        foreach (var place in Places)
        {
          svg.Add(new XElement(SvgUtil.Ns + "rect",
            new XAttribute("x", diagram.DateLocation(place.End)),
            new XAttribute("y", top),
            new XAttribute("width", diagram.DateLocation(place.Start) - diagram.DateLocation(place.End)),
            new XAttribute("height", height),
            new XAttribute("style", $"fill:{diagram.Color(place.PlaceName)};"),
            new XAttribute("title", place.PlaceName)
          ));
        }
        var ancestorHeight = height / Ancestors.Count;
        var currTop = top;
        foreach (var ancestor in Ancestors)
        {
          ancestor.AddInformation(svg, diagram, currTop, ancestorHeight);
          currTop += ancestorHeight;
        }
      }
    }

    private class Diagram
    {
      private Dictionary<string, int> _placeHistogram = new Dictionary<string, int>();
      
      public double Width { get; set; }
      public double Height { get; set; }
      public DateTime LeftDate { get; set; }
      public DateTime RightDate { get; set; } = DateTime.MaxValue;

      public Dictionary<string, string> PlaceColors()
      {
        return _placeHistogram.Keys.ToDictionary(k => k, k => Color(k));
      }

      public void AddPlace(PlaceRange place)
      {
        if (place.Start < RightDate)
          RightDate = place.Start;
        if (!_placeHistogram.TryGetValue(place.PlaceName, out var count))
          count = 1;
        _placeHistogram[place.PlaceName] = count;
      }

      public double DateLocation(DateTime dateTime)
      {
        return (LeftDate - dateTime).TotalDays / (LeftDate - RightDate).TotalDays * Width;
      }

      public string Color(string placeName)
      {
        return ReportStyle.Default.Colors[_placeHistogram.OrderByDescending(k => k.Value).Select(k => k.Key).ToList().IndexOf(placeName)];
      }
    }
  }
}
