using GedcomParser.Model;
using GedcomParser.Renderer;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using System;
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

      var personIndex = new Lookup<Individual, Reference>();

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
            html.WriteString(": ");
            resolvedEvent.Description(html);
            html.WriteEndElement();
          }
          html.WriteEndElement();
        }

        _extension.SectionIds.Pop();
        html.WriteEndElement();
      }

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
