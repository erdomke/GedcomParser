using GedcomParser.Model;
using GedcomParser.Renderer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GedcomParser
{
  internal class AncestorFamilySection : IFamilySection
  {
    private SourceListSection _sourceList;

    public string Title { get; }

    public List<AncestorGroup> Groups { get; } = new List<AncestorGroup>();

    public string Id => Groups.First().Families.First().Id.Primary;

    public ExtendedDateTime StartDate { get; set; }

    public IEnumerable<ResolvedFamily> AllFamilies => Groups.SelectMany(g => g.Families);

    public IEnumerable<Media> Media { get; set; }

    public AncestorFamilySection(string title, IEnumerable<string> rootIds, SourceListSection sourceList)
    {
      Title = title;
      Groups.AddRange(rootIds.Select(i => new AncestorGroup(i)));
      foreach (var group in Groups)
        group.NextPeople.Add(group.RootPersonId);
      _sourceList = sourceList;
    }

    public void Render(HtmlTextWriter html, ReportRenderer renderer)
    {
      var paraBuilder = new ParagraphBuilder()
      {
        SourceList = _sourceList,
        DirectAncestors = renderer.DirectAncestors
      };
      html.WriteStartSection(this);

      foreach (var family in Groups.SelectMany(g => g.Families).Skip(1))
      {
        html.WriteStartElement("a");
        html.WriteAttributeString("id", family.Id.Primary);
        html.WriteEndElement();
      }

      DescendentFamilySection.RenderIntro(this, html, renderer);

      var baseDirectory = Path.GetDirectoryName(renderer.Database.BasePath);

      foreach (var group in Groups)
      {
        html.WriteStartElement("div");
        html.WriteAttributeString("class", "diagrams");

        html.WriteStartElement("figure");
        html.WriteAttributeString("class", "ancestors");
        var ancestorRenderer = new AncestorRenderer(group.Families, group.RootPersonId)
        {
          Graphics = renderer.Graphics
        };
        var svg = ancestorRenderer.Render();
        svg.SetAttributeValue("style", "max-width:7.5in;max-height:9in");
        svg.WriteTo(html);
        html.WriteEndElement();

        var mapRenderer = new MapRenderer();
        if (mapRenderer.TryRender(group.Families, baseDirectory, out var figures))
        {
          foreach (var figure in figures)
          {
            html.WriteStartElement("figure");
            html.WriteAttributeString("class", "map");
            figure.Map.WriteTo(html);
            if (!string.IsNullOrEmpty(figure.Caption))
            {
              html.WriteStartElement("figcaption");
              html.WriteAttributeString("style", $"max-width:{Math.Max(2.5 * 96, figure.Width)}px;");
              html.WriteString(figure.Caption);
              html.WriteEndElement();
            }
            html.WriteEndElement();
          }
        }

        foreach (var family in group.Families)
        {
          var allEvents = ResolvedEventGroup.Group(family.Events
            .Where(e => renderer.DirectAncestors.Intersect(e.Primary.SelectMany(e => e.Id)).Any()
              || e.Event.Type == EventType.Birth
              || e.Event.Type == EventType.Adoption
              || e.Event.Type == EventType.Death));
          paraBuilder.StartParagraph(html);
          foreach (var ev in allEvents)
            paraBuilder.WriteEvent(html, ev, true);
          paraBuilder.EndParagraph(html);

          DescendentFamilySection.RenderGallery(html, family.Media
                      .Concat(family.Events.SelectMany(e => e.Event.Media.Concat(e.Related.SelectMany(r => r.Media)))), renderer.Graphics);
        }

        html.WriteEndElement();
      }
    }
  }

  internal record AncestorGroup(string RootPersonId)
  {
    public List<ResolvedFamily> Families { get; } = new List<ResolvedFamily>();
    public List<string> NextPeople { get; } = new List<string>();
  }
}
