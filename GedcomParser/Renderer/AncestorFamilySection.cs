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

    public void Render(HtmlTextWriter html, ReportRenderer renderer, RenderState state)
    {
      var paraBuilder = new ParagraphBuilder()
      {
        SourceList = _sourceList,
        DirectAncestors = renderer.DirectAncestors,
        IncludeBurialInformation = false,
        IncludeAges = false,
        MonthStyle = "MMM"
      };
      html.WriteStartSection(this, state);

      foreach (var family in Groups.SelectMany(g => g.Families).Skip(1))
      {
        html.WriteStartElement("a");
        html.WriteAttributeString("id", family.Id.Primary);
        html.WriteEndElement();
      }

      DescendentFamilySection.RenderIntro(this, html, renderer, _sourceList);

      var baseDirectory = Path.GetDirectoryName(renderer.Database.BasePath);

      foreach (var group in Groups)
      {
        html.WriteElementString("h3", renderer.Database.GetValue<Individual>(group.RootPersonId).Name.Name);

        html.WriteStartElement("div");
        html.WriteAttributeString("class", "diagrams");

        html.WriteStartElement("figure");
        html.WriteAttributeString("class", "ancestors");
        var ancestorRenderer = new AncestorRenderer(group.Families, group.RootPersonId)
        {
          Graphics = renderer.Graphics
        };
        var svg = ancestorRenderer.Render();
        svg.SetAttributeValue("style", $"max-width:{ReportStyle.Default.PageWidthInches}in;max-height:8in");
        svg.WriteTo(html);
        html.WriteEndElement();

        var peopleWithPictures = group.Families
          .SelectMany(f => f.Members.Select(m => m.Individual))
          .Where(i => i.Picture != null
            && i.Id.Intersect(renderer.DirectAncestors).Any())
          .Distinct()
          .ToList();

        foreach (var person in peopleWithPictures)
        {
          html.WriteStartElement("figure");
          html.WriteStartElement("img");
          html.WriteAttributeString("src", person.Picture.Src);
          html.WriteAttributeString("style", $"width:{person.Picture.Width * 100 / person.Picture.Height:0.0}px;height:100px");
          if (person.Picture.Attributes.TryGetValue("grayscale", out var value) && value == "true")
            html.WriteAttributeString("class", "grayscale");
          html.WriteEndElement();

          html.WriteStartElement("figcaption");
          html.WriteString(person.Name.Name);
          html.WriteEndElement();

          html.WriteEndElement();
        }

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

        html.WriteEndElement();

        GalleryRenderer.Render(html, group.Families
          .SelectMany(f => f.Media
            .Concat(f.Events
              .Where(e => e.Event.Type != EventType.Death)
              .SelectMany(e => e.Event.Media
                .Concat(e.Related.SelectMany(r => r.Media))
              )
            )
          ), renderer.Graphics, _sourceList);
      }
    }
  }

  internal record AncestorGroup(string RootPersonId)
  {
    public List<ResolvedFamily> Families { get; } = new List<ResolvedFamily>();
    public List<string> NextPeople { get; } = new List<string>();
  }
}
