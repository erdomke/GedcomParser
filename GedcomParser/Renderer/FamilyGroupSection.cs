using GedcomParser.Model;
using GedcomParser.Renderer;
using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GedcomParser
{
  internal class FamilyGroupSection : ISection
  {
    private const double minCaptionWidth = 2.5 * 96;

    private SourceListSection _sourceList;

    public string Title { get; set; }
    public List<ResolvedFamily> Families { get; } = new List<ResolvedFamily>();
    public string Id => Families.First().Id.Primary;

    public static IEnumerable<FamilyGroupSection> Create(IEnumerable<ResolvedFamily> resolvedFamilies, ReportOptions options, SourceListSection sourceList)
    {
      var xref = new Dictionary<string, ResolvedFamily>();
      foreach (var family in resolvedFamilies)
      {
        foreach (var id in family.Id)
          xref.Add(id, family);
      }

      var result = options.Groups
        .Select(g =>
        {
          var resolved = new FamilyGroupSection()
          {
            Title = g.Title,
            _sourceList = sourceList
          };
          resolved.Families.AddRange(g.Ids
            .Select(i => xref.TryGetValue(i, out var f) ? f : null)
            .Where(f => f != null)
            .Distinct()
            .OrderBy(f => f.StartDate));
          if (string.IsNullOrEmpty(resolved.Title))
            resolved.Title = string.Join(" + ", resolved.Families.SelectMany(f => f.Parents).Select(p => p.Name.Surname).Distinct());
          return resolved;
        })
        .ToList();

      foreach (var id in result.SelectMany(g => g.Families).SelectMany(f => f.Id))
        xref.Remove(id);

      foreach (var family in xref.Values)
      {
        var resolved = new FamilyGroupSection()
        {
          Title = string.Join(" + ", family.Parents.Select(p => p.Name.Surname)),
          _sourceList = sourceList
        };
        resolved.Families.Add(family);
        result.Add(resolved);
      }

      return result.OrderByDescending(g => g.Families.First().StartDate).ToList();
    }

    public void Render(HtmlTextWriter html, FencedDivExtension extension)
    {
      html.WriteStartSection(this);

      foreach (var family in Families.Skip(1))
      {
        html.WriteStartElement("a");
        html.WriteAttributeString("id", family.Id.Primary);
        html.WriteEndElement();
      }

      var baseDirectory = Path.GetDirectoryName(extension.Database.BasePath);

      html.WriteStartElement("div");
      html.WriteAttributeString("class", "diagrams");

      html.WriteStartElement("figure");
      html.WriteAttributeString("class", "decendants");
      var decendantRenderer = new DecendantLayout()
      {
        Graphics = extension.Graphics
      };
      decendantRenderer.Render(Families, baseDirectory).WriteTo(html);
      html.WriteEndElement();

      var mapRenderer = new MapRenderer();
      if (mapRenderer.TryRender(Families, baseDirectory, out var figure))
      {
        html.WriteStartElement("figure");
        html.WriteAttributeString("class", "map");
        figure.Map.WriteTo(html);
        if (!string.IsNullOrEmpty(figure.Caption))
        {
          html.WriteStartElement("figcaption");
          html.WriteAttributeString("style", $"max-width:{Math.Max(minCaptionWidth, figure.Width)}px;");
          html.WriteString(figure.Caption);
          html.WriteEndElement();
        }
        html.WriteEndElement();
      }

      html.WriteStartElement("figure");
      html.WriteAttributeString("class", "timeline");
      var timelineRenderer = new TimelineRenderer()
      {
        Graphics = extension.Graphics
      };
      timelineRenderer.Render(Families, baseDirectory).WriteTo(html);
      html.WriteEndElement();

      html.WriteEndElement();

      foreach (var family in Families)
      {
        html.WriteStartElement("p");  
        var firstEvent = true;

        var allEvents = ResolvedEventGroup.Group(family.Events
          .Where(e => e.Event.Date.HasValue
            && e.Event.TypeString != "Arrival"
            && e.Event.TypeString != "Departure"
            && !(e.Event.Type == EventType.Residence && e.Event.Place == null))
          .OrderBy(e => e.Event.Date));

        foreach (var ev in allEvents)
        {
          if (firstEvent)
            firstEvent = false;
          else
            html.WriteString(" ");
          ev.Description(html, _sourceList, true);
        }
        html.WriteEndElement();

        RenderGallery(html, family.Family.Media
          .Concat(family.Events.SelectMany(e => e.Event.Media)), true);
      }

      /*var events = Families.SelectMany(f => f.Events)
        .Where(e => e.Event.Date.HasValue)
        .OrderBy(e => e.Event.Date)
        .ToList();
      if (events.Count > 0)
      {
        html.WriteStartElement("ul");
        foreach (var resolvedEvent in events)
        {
          html.WriteStartElement("li");
          html.WriteStartElement("div");
          html.WriteAttributeString("class", "gallery");

          html.WriteStartElement("div");
          html.WriteAttributeString("class", "event-descrip");
          html.WriteStartElement("time");
          html.WriteString(resolvedEvent.Event.Date.ToString("yyyy MMM d"));
          html.WriteEndElement();
          html.WriteString(": ");
          resolvedEvent.Description(html);

          var eventCitations = resolvedEvent.Event.Citations
            .Select(c => new { Id = c.Id.Primary, Index = _sourceList.Citations.IndexOf(c) })
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
          html.WriteEndElement();

          RenderGallery(html, resolvedEvent.Event.Media
            .Concat(resolvedEvent.Related.SelectMany(m => m.Media)), false);

          html.WriteEndElement();
          html.WriteEndElement();
        }
        html.WriteEndElement();
      } */

      html.WriteEndElement();
    }

    private static HashSet<string> _imageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
      ".png", ".gif", ".bmp", ".jpg", ".jpeg"
    };

    private void RenderGallery(HtmlTextWriter html, IEnumerable<Media> media, bool inGallery)
    {
      var images = media
        .Where(m => !string.IsNullOrEmpty(m.Src)
          && _imageExtensions.Contains(Path.GetExtension(m.Src))
          && !(m.Attributes.TryGetValue("hidden", out var hidden) && hidden == "true"))
        .Distinct()
        .OrderBy(m => m.Date.HasValue ? 0 : 1)
        .ThenBy(m => m.Date)
        .ToList();
      if (images.Count > 0)
      {
        if (inGallery)
        {
          html.WriteStartElement("div");
          html.WriteAttributeString("class", "gallery");
        }
        foreach (var image in images)
        {
          html.WriteStartElement("figure");
          if (image.Width.HasValue)
          {
            var height = Math.Min(3, image.Height.Value / 96);
            html.WriteAttributeString("style", $"width:max({minCaptionWidth}px, {image.Width.Value * height / image.Height.Value}in)");
          }
          html.WriteStartElement("img");
          html.WriteAttributeString("src", image.Src);
          html.WriteEndElement();

          if (!string.IsNullOrEmpty(image.Description)
            || image.Date.HasValue
            || image.Place != null)
          {
            html.WriteStartElement("figcaption");
            if (inGallery && image.Date.HasValue)
            {
              html.WriteStartElement("time");
              html.WriteString(image.Date.ToString("yyyy MMM d"));
              html.WriteEndElement();
              html.WriteString(": ");
            }
            html.WriteString(image.Description);
            if (inGallery && image.Place != null)
            {
              html.WriteString(" at " + image.Place.Names.FirstOrDefault()?.Name);
            }
            html.WriteString(".");
            html.WriteEndElement();
          }
          html.WriteEndElement();
        }
        if (inGallery)
          html.WriteEndElement();
      }
    }
  }
}
