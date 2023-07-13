using GedcomParser.Model;
using GedcomParser.Renderer;
using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace GedcomParser
{
  internal class FamilyGroupSection : ISection
  {
    private SourceListSection _sourceList;

    public string Title { get; set; }
    public List<ResolvedFamily> Families { get; } = new List<ResolvedFamily>();
    public string Id => Families.First().Id.Primary;

    public static IEnumerable<FamilyGroupSection> Create(IEnumerable<ResolvedFamily> resolvedFamilies, IEnumerable<FamilyGroup> groups, SourceListSection sourceList)
    {
      var xref = new Dictionary<string, ResolvedFamily>();
      foreach (var family in resolvedFamilies)
      {
        foreach (var id in family.Id)
          xref.Add(id, family);
      }

      var result = groups
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

    public void Render(HtmlTextWriter html, ReportRenderer renderer)
    {
      var paraBuilder = new ParagraphBuilder()
      {
        SourceList = _sourceList,
        DirectAncestors = renderer.DirectAncestors
      };
      html.WriteStartSection(this);

      foreach (var family in Families.Skip(1))
      {
        html.WriteStartElement("a");
        html.WriteAttributeString("id", family.Id.Primary);
        html.WriteEndElement();
      }

      var baseDirectory = Path.GetDirectoryName(renderer.Database.BasePath);

      html.WriteStartElement("div");
      html.WriteAttributeString("class", "diagrams");

      html.WriteStartElement("figure");
      html.WriteAttributeString("class", "decendants");
      var decendantRenderer = new DecendantLayout()
      {
        Graphics = renderer.Graphics
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
          html.WriteAttributeString("style", $"max-width:{Math.Max(2.5*96, figure.Width)}px;");
          html.WriteString(figure.Caption);
          html.WriteEndElement();
        }
        html.WriteEndElement();
      }

      html.WriteStartElement("figure");
      html.WriteAttributeString("class", "timeline");
      var timelineRenderer = new TimelineRenderer()
      {
        Graphics = renderer.Graphics
      };
      timelineRenderer.Render(Families, baseDirectory).WriteTo(html);
      html.WriteEndElement();

      html.WriteEndElement();

      foreach (var family in Families)
      {
        var allEvents = ResolvedEventGroup.Group(family.Events
          .Where(e => e.Event.Date.HasValue
            && e.Event.TypeString != "Arrival"
            && e.Event.TypeString != "Departure"
            && !(e.Event.Type == EventType.Residence && e.Event.Place == null))
          .OrderBy(e => e.Event.Date));

        paraBuilder.StartParagraph(html);
        foreach (var ev in allEvents)
          paraBuilder.WriteEvent(html, ev, true);
        paraBuilder.EndParagraph(html);

        RenderGallery(html, family.Media
          .Concat(family.Events.SelectMany(e => e.Event.Media.Concat(e.Related.SelectMany(r => r.Media)))), renderer.Graphics);
      }

      html.WriteEndElement();
    }

    private static HashSet<string> _imageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
      ".png", ".gif", ".bmp", ".jpg", ".jpeg"
    };

    private const double targetHeight = 3.2 * 96;
    private const double lineWidth = 7 * 96;
    private const double gap = 8;

    private void RenderGallery(HtmlTextWriter html, IEnumerable<Media> media, IGraphics graphics)
    {
      var articles = media
        .Where(m => string.IsNullOrEmpty(m.Src)
          && !string.IsNullOrEmpty(m.Description)
          && !(m.Attributes.TryGetValue("hidden", out var hidden) && hidden == "true"))
        .OrderBy(m => m.TopicDate)
        .ToList();
      foreach (var article in articles)
      {
        html.WriteStartElement("article");
        html.WriteRaw(Markdig.Markdown.ToHtml(article.Description));
        html.WriteEndElement();
      }

      var images = media
        .Where(m => !string.IsNullOrEmpty(m.Src)
          && _imageExtensions.Contains(Path.GetExtension(m.Src))
          && !(m.Attributes.TryGetValue("hidden", out var hidden) && hidden == "true"))
        .Distinct()
        .OrderBy(m => m.Date.HasValue ? 0 : 1)
        .ThenBy(m => m.Date)
        .Select(m => new ImageBox(m, graphics))
        .ToList();
      if (images.Count > 0)
      {
        var rowWidth = 0.0;
        var count = 0;
        var start = 0;
        for (var i = 0; i < images.Count; i++)
        {
          rowWidth += images[i].BoxWidth;
          count++;
          if (count >= 2 && rowWidth + (count - 1) * gap > lineWidth)
          {
            var minWidth = images.Skip(start).Take(count).Sum(im => im.CaptionWidth) + (count - 1) * gap;
            while (count > 2 && minWidth > lineWidth)
            {
              count--;
              i--;
              minWidth = images.Skip(start).Take(count).Sum(im => im.CaptionWidth) + (count - 1) * gap;
            }

            var totalWidth = images.Skip(start).Take(count).Sum(im => im.BoxWidth) + (count - 1) * gap;
            var numAttempts = 0;
            while (totalWidth > lineWidth && numAttempts < 10)
            {
              var factor = Math.Min(Math.Floor(lineWidth / totalWidth * 1000) / 1000, 0.99);
              foreach (var image in images.Skip(start).Take(count))
              {
                image.ImageHeight *= factor;
                image.ImageWidth *= factor;
              }
              totalWidth = images.Skip(start).Take(count).Sum(im => im.BoxWidth) + (count - 1) * gap;
              numAttempts++;
            }

            start = i + 1;
            count = 0;
            rowWidth = 0;
          }
        }

        html.WriteStartElement("div");
        html.WriteAttributeString("class", "gallery");
        foreach (var image in images)
        {
          html.WriteStartElement("figure");
          html.WriteAttributeString("style", $"max-width:{image.BoxWidth}px;");
          html.WriteStartElement("img");
          html.WriteAttributeString("src", image.Media.Src);
          html.WriteAttributeString("style", $"width:{image.ImageWidth}px;height:{image.ImageHeight}px");
          html.WriteEndElement();

          image.WriteCaption(html);
          html.WriteEndElement();
        }
        html.WriteEndElement();
      }
    }

    private class ImageBox
    {
      public Media Media { get; }
      public double CaptionWidth { get; } = 2 * 96;
      public double ImageWidth { get; set; }
      public double ImageHeight { get; set; }
      public double BoxWidth => Math.Max(ImageWidth, CaptionWidth);

      public ImageBox(Media media, IGraphics graphics)
      {
        Media = media;
        ImageHeight = targetHeight;
        if (!media.Width.HasValue || !media.Height.HasValue)
          ImageWidth = targetHeight;
        else
          ImageWidth = media.Width.Value * targetHeight / media.Height.Value;
        var root = new XElement("root");
        using (var writer = root.CreateWriter())
          WriteCaption(writer);
        var captionText = string.Join("", root.DescendantNodes().OfType<XText>().Select(t => t.Value));
        if (string.IsNullOrEmpty(captionText))
        {
          CaptionWidth = 0;
        }
        else
        {
          var captionSize = graphics.MeasureText(ReportStyle.Default.FontName, ReportStyle.Default.BaseFontSize, captionText);
          CaptionWidth = Math.Min(captionSize.Width, CaptionWidth);
        }
      }

      public void WriteCaption(XmlWriter html)
      {
        if (!string.IsNullOrEmpty(Media.Description)
            || Media.Date.HasValue
            || Media.Place != null)
        {
          html.WriteStartElement("figcaption");
          if (Media.Date.HasValue)
          {
            html.WriteStartElement("time");
            html.WriteString(Media.Date.ToString("yyyy MMM d"));
            html.WriteEndElement();
            html.WriteString(": ");
          }
          if (!string.IsNullOrEmpty(Media.Description))
            html.WriteRaw(ParagraphBuilder.ToInlineHtml(Media.Description.TrimEnd('.')));
          if (Media.Place != null)
          {
            html.WriteString(" at " + Media.Place.Names.FirstOrDefault()?.Name);
          }
          html.WriteString(".");
          html.WriteEndElement();
        }
      }
    }
  }
}
