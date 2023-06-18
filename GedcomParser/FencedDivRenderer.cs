using GedcomParser.Model;
using GedcomParser.Renderer;
using Markdig.Extensions.Figures;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace GedcomParser
{
  internal class FencedDivRenderer : HtmlObjectRenderer<FencedDiv>
  {
    private const double minCaptionWidth = 2.5 * 96;
    private readonly FencedDivExtension _extension;

    public FencedDivRenderer(FencedDivExtension extension)
    {
      _extension = extension;
    }

    protected override void Write(HtmlRenderer renderer, FencedDiv obj)
    {
      var html = new HtmlTextWriter(renderer.Writer);

      var personIndex = new Lookup<Individual, Reference>();

      var citations = _extension.ResolvedFamilies()
        .SelectMany(f => f.Events)
        .SelectMany(e => e.Event.Citations)
        .Distinct()
        .OrderBy(c =>
        {
          var builder = new StringBuilder();
          c.BuildEqualityString(builder, null);
          return builder.ToString();
        }, StringComparer.OrdinalIgnoreCase)
        .ToList();

      var groups = FamilyGroup.Create(_extension.ResolvedFamilies(), obj.Options);
      foreach (var group in groups)
      {
        var reference = new Reference(group.Families[0].Id.Primary, group.Title);
        foreach (var individual in group.Families
          .SelectMany(f => f.Members.Select(m => m.Individual))
          .Distinct())
        {
          personIndex.Add(individual, reference);
        }

        html.WriteStartElement("section");
        html.WriteAttributeString("class", "chapter");
        html.WriteStartElement("h2");
        _extension.SectionIds.Push(group.Families.Select(f => f.Id.Primary).ToList());
        if (group.Families.Count == 1)
          html.WriteAttributeString("id", group.Families[0].Id.Primary);
        html.WriteString(group.Title);
        html.WriteEndElement();

        if (group.Families.Count > 1)
        {
          foreach (var family in group.Families)
          {
            html.WriteStartElement("a");
            html.WriteAttributeString("id", family.Id.Primary);
            html.WriteEndElement();
          }
        }

        var baseDirectory = Path.GetDirectoryName(_extension.Database.BasePath);

        html.WriteStartElement("div");
        html.WriteAttributeString("class", "diagrams");

        html.WriteStartElement("figure");
        html.WriteAttributeString("class", "decendants");
        var decendantRenderer = new DecendantLayout()
        {
          Graphics = _extension.Graphics
        };
        decendantRenderer.Render(group.Families, baseDirectory).WriteTo(html);
        html.WriteEndElement();

        var mapRenderer = new MapRenderer();
        if (mapRenderer.TryRender(group.Families, baseDirectory, out var figure))
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
          Graphics = _extension.Graphics
        };
        timelineRenderer.Render(group.Families, baseDirectory).WriteTo(html);
        html.WriteEndElement();

        html.WriteEndElement();

        var events = group.Families.SelectMany(f => f.Events)
          .Where(e => e.Event.Date.HasValue)
          .OrderBy(e => e.Event.Date)
          .ToList();
        if (events.Count > 0)
        {
          html.WriteStartElement("ul");
          foreach (var resolvedEvent in events)
          {
            html.WriteStartElement("li");
            html.WriteStartElement("time");
            html.WriteString(resolvedEvent.Event.Date.ToString("yyyy MMM d"));
            html.WriteEndElement();
            html.WriteString(": ");
            resolvedEvent.Description(html);

            var eventCitations = resolvedEvent.Event.Citations
              .Select(c => new { Id = c.Id.Primary, Index = citations.IndexOf(c) })
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

            RenderGallery(html, resolvedEvent.Event.Media
              .Concat(resolvedEvent.Related.SelectMany(m => m.Media)));

            html.WriteEndElement();
          }
          html.WriteEndElement();
        }

        RenderGallery(html, group.Families
          .SelectMany(f => f.Family.Media
            //.Concat(f.Members.SelectMany(m => m.Individual.Media))
          ));

        _extension.SectionIds.Pop();
        html.WriteEndElement();
      }

      RenderPeopleIndex(html, personIndex);

      RenderSourceList(html, citations);
    }

    private static HashSet<string> _imageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
      ".png", ".gif", ".bmp", ".jpg", ".jpeg"
    };

    private void RenderGallery(HtmlTextWriter html, IEnumerable<Media> media)
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
        html.WriteStartElement("div");
        html.WriteAttributeString("class", "gallery");
        foreach (var image in images)
        {
          html.WriteStartElement("figure");
          html.WriteStartElement("img");
          html.WriteAttributeString("src", image.Src);
          html.WriteEndElement();

          if (!string.IsNullOrEmpty(image.Description)
            || image.Date.HasValue
            || image.Place != null)
          {
            html.WriteStartElement("figcaption");
            if (image.Width.HasValue)
            {
              var width = image.Width.Value * (2 * 96) / image.Height.Value;
              html.WriteAttributeString("style", $"max-width:{Math.Max(minCaptionWidth, width)}px;");
            }
            if (image.Date.HasValue)
            {
              html.WriteStartElement("time");
              html.WriteString(image.Date.ToString("yyyy MMM d"));
              html.WriteEndElement();
              html.WriteString(": ");
            }
            html.WriteString(image.Description);
            if (image.Place != null)
            {
              html.WriteString(" at " + image.Place.Names.FirstOrDefault()?.Name);
            }
            html.WriteString(".");
            html.WriteEndElement();
          }

          html.WriteEndElement();
        }
        html.WriteEndElement();
      }
    }

    private void RenderPeopleIndex(HtmlTextWriter html, ILookup<Individual, Reference> personIndex)
    {
      html.WriteStartElement("section");
      html.WriteElementString("h2", "Person Index");
      foreach (var person in personIndex
        .OrderBy(p => p.Key.Names.First().Surname ?? p.Key.Name.Surname, StringComparer.OrdinalIgnoreCase)
        .ThenBy(p => p.Key.Name.Name, StringComparer.OrdinalIgnoreCase))
      {
        html.WriteStartElement("div");
        html.WriteAttributeString("id", person.Key.Id.Primary);
        html.WriteAttributeString("class", "person-index");
        html.WriteStartElement("div");
        html.WriteAttributeString("class", "person");
        html.WriteElementString("strong", person.Key.Name.Surname);
        html.WriteString(", " + person.Key.Name.Remaining);
        if (person.Key.BirthDate.HasValue || person.Key.DeathDate.HasValue)
        {
          html.WriteStartElement("span");
          html.WriteAttributeString("style", "color:#999;");
          html.WriteString($" ({person.Key.DateString})");
          html.WriteEndElement();
        }
        html.WriteEndElement();
        html.WriteStartElement("div");
        html.WriteAttributeString("class", "filler");
        html.WriteEndElement();
        html.WriteStartElement("div");
        html.WriteAttributeString("class", "refs");
        var first = true;
        foreach (var reference in person)
        {
          if (first)
            first = false;
          else
            html.WriteString(", ");
          html.WriteStartElement("a");
          html.WriteAttributeString("href", "#" + reference.Id);
          html.WriteString(reference.Title);
          html.WriteEndElement();
        }
        html.WriteEndElement();
        html.WriteEndElement();
      }
      html.WriteEndElement();
      html.WriteEndElement();
    }

    private void RenderSourceList(HtmlTextWriter html, IEnumerable<Citation> citations)
    {
      html.WriteStartElement("section");
      html.WriteElementString("h2", "Source List");

      html.WriteStartElement("ol");
      html.WriteAttributeString("class", "source-list");
      foreach (var citation in citations)
      {
        html.WriteStartElement("li");
        html.WriteAttributeString("id", citation.Id.Primary);
        citation.WriteBibliographyEntry(html);
        html.WriteEndElement();
      }
      html.WriteEndElement();

      html.WriteEndElement();
    }

    private class Reference
    {
      public string Id { get; }
      public string Title { get; }

      public Reference(string id, string title)
      {
        Id = id;
        Title = title;
      }
    }

    private class FamilyGroup
    {
      public string Title { get; set; }
      public List<ResolvedFamily> Families { get; } = new List<ResolvedFamily>();

      public static IEnumerable<FamilyGroup> Create(IEnumerable<ResolvedFamily> resolvedFamilies, ReportOptions options)
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
            var resolved = new FamilyGroup()
            {
              Title = g.Title
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
          var resolved = new FamilyGroup()
          {
            Title = string.Join(" + ", family.Parents.Select(p => p.Name.Surname))
          };
          resolved.Families.Add(family);
          result.Add(resolved);
        }

        return result.OrderByDescending(g => g.Families.First().StartDate).ToList();
      }
    }
  }
}
