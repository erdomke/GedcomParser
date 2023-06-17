using GedcomParser.Model;
using GedcomParser.Renderer;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GedcomParser
{
  internal class FencedDivRenderer : HtmlObjectRenderer<FencedDiv>
  {
    private readonly FencedDivExtension _extension;

    public FencedDivRenderer(FencedDivExtension extension)
    {
      _extension = extension;
    }

    protected override void Write(HtmlRenderer renderer, FencedDiv obj)
    {
      var html = new HtmlTextWriter(renderer.Writer);

      var groups = FamilyGroup.Create(_extension.ResolvedFamilies(), obj.Options);
      foreach (var group in groups)
      {
        html.WriteStartElement("section");
        html.WriteAttributeString("class", "chapter");
        html.WriteStartElement("h2");
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

        html.WriteStartElement("figure");
        var decendantRenderer = new DecendantLayout()
        {
          Graphics = _extension.Graphics
        };
        decendantRenderer.Render(group.Families, baseDirectory).WriteTo(html);
        html.WriteEndElement();

        html.WriteStartElement("figure");
        var timelineRenderer = new TimelineRenderer()
        {
          Graphics = _extension.Graphics
        };
        timelineRenderer.Render(group.Families, baseDirectory).WriteTo(html);
        html.WriteEndElement();

        var mapRenderer = new MapRenderer();
        if (mapRenderer.TryRender(group.Families, baseDirectory, out var map))
        {
          html.WriteStartElement("figure");
          map.WriteTo(html);
          html.WriteEndElement();
        }

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
            html.WriteString(": " + resolvedEvent.Description());
            html.WriteEndElement();
          }
          html.WriteEndElement();
        }

        html.WriteEndElement();
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
