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
  internal class DescendentFamilySection : IFamilySection
  {
    private SourceListSection _sourceList;
    
    public string Title { get; set; }
    public List<ResolvedFamily> Families { get; } = new List<ResolvedFamily>();
    public string Id => Families.First().Id.Primary;
    public ExtendedDateTime StartDate { get; set; }
    public bool HighlightsOnly { get; set; }
    IEnumerable<ResolvedFamily> IFamilySection.AllFamilies => Families;

    public DescendentFamilySection(string title, SourceListSection sourceList)
    {
      Title = title;
      _sourceList = sourceList;
    }

    public static IEnumerable<IFamilySection> Create(IEnumerable<ResolvedFamily> resolvedFamilies, IEnumerable<FamilyGroup> groups, SourceListSection sourceList)
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
          
          if (g.Type == FamilyGroupType.Ancestors)
          {
            return (IFamilySection)new AncestorFamilySection(g.Title, g.Ids, sourceList)
            {
              StartDate = g.TopicDate
            };
          }
          else
          {
            var resolved = new DescendentFamilySection(g.Title, sourceList)
            {
              HighlightsOnly = g.Type == FamilyGroupType.DescendantHighlights
            };
            resolved.Families.AddRange(g.Ids
              .Select(i => xref.TryGetValue(i, out var f) ? f : null)
              .Where(f => f != null)
              .Distinct()
              .OrderBy(f => f.StartDate));
            if (string.IsNullOrEmpty(resolved.Title))
              resolved.Title = string.Join(" + ", resolved.Families.SelectMany(f => f.Parents).Select(p => p.Name.Surname).Distinct());
            resolved.StartDate = g.TopicDate.HasValue ? g.TopicDate : resolved.Families.First().StartDate;
            return resolved;
          }
        })
        .ToList();

      var ancestors = result.OfType<AncestorFamilySection>()
        .SelectMany(a => a.Groups)
        .ToList();
      var assignedFamilies = new HashSet<string>(result
        .OfType<DescendentFamilySection>()
        .SelectMany(s => s.Families)
        .Select(f => f.Id.Primary));
      while (true)
      {
        var incomplete = ancestors.Where(a => a.NextPeople.Count > 0).ToList();
        if (incomplete.Count < 1)
          break;

        foreach (var group in incomplete)
        {
          var newFamilies = group.NextPeople
            .SelectMany(i => resolvedFamilies
              .Where(f => !assignedFamilies.Contains(f.Id.Primary)
                && f.Members.Any(m => m.Individual.Id.Contains(i) && m.Role.HasFlag(FamilyLinkType.Child))))
            .ToList();
          group.Families.AddRange(newFamilies);
          assignedFamilies.UnionWith(newFamilies.Select(f => f.Id.Primary));
          var nextPeople = newFamilies
            .Where(f => f.Children(FamilyLinkType.Birth).Any(i => i.Id.Intersect(group.NextPeople).Any()))
            .SelectMany(f => f.Parents)
            .Select(i => i.Id.Primary)
            .ToList();
          group.NextPeople.Clear();
          group.NextPeople.AddRange(nextPeople);
        }
      }

      foreach (var group in ancestors)
      {
        var ordered = group.Families
          .GroupBy(f => f.Members.Any(i => i.Individual.Id.Primary.Contains(group.RootPersonId)))
          .SelectMany(g => g.Key ? g.OrderBy(f => f.StartDate) : g.OrderByDescending(f => f.StartDate))
          .ToList();
        group.Families.Clear();
        group.Families.AddRange(ordered);
      }
      foreach (var section in result.OfType<AncestorFamilySection>())
      {
        section.Groups.Sort((x, y) => x.Families.First().StartDate.CompareTo(y.Families.First().StartDate) * -1);
        if (!section.StartDate.HasValue)
          section.StartDate = section.Groups.Min(g => g.Families.First().StartDate);
      }

      return result.OrderByDescending(g => g.StartDate).ToList();
    }

    internal static void RenderIntro(IFamilySection section, HtmlTextWriter html, ReportRenderer renderer)
    {
      var eventDates = section.AllFamilies
        .SelectMany(f => f.Events)
        .Select(e => e.Event.Date)
        .Where(d => d.HasValue)
        .OrderBy(d => d)
        .ToList();
      var startYear = eventDates[0].Start.Year;
      var endYear = eventDates.Last().End.HasValue ? eventDates.Last().End.Year : eventDates.Last().Start.Year;
      var crossReferencePeople = renderer.PersonIndex.Index
        .Where(g => g.Contains(section) && g.Skip(1).Any())
        .SelectMany(g => CreateLinks(g, section))
        .ToList();
      var leftPeople = crossReferencePeople
        .Where(l => l.Left)
        .GroupBy(l => l.Section)
        .ToList();
      var rightPeople = crossReferencePeople
        .Where(l => !l.Left)
        .GroupBy(l => l.Section)
        .ToList();

      void RenderLinks(IEnumerable<IGrouping<ISection, PersonLink>> sections)
      {
        foreach (var section in sections)
        {
          html.WriteString("See ");
          html.WriteStartElement("a");
          html.WriteAttributeString("href", "#" + section.Key.Id);
          html.WriteAttributeString("style", "font-style:italic");
          html.WriteString(section.Key.Title);
          html.WriteEndElement();
          html.WriteString(" for more information about");
          var last = section.Count() - 1;
          var i = 0;
          foreach (var link in section)
          {
            if (i == 0)
              html.WriteString(" ");
            else if (i == last)
              html.WriteString(i > 1 ? ", and " : " and ");
            else
              html.WriteString(", ");
            html.WriteStartElement("a");
            html.WriteAttributeString("href", "#" + link.Individual.Id.Primary);
            html.WriteAttributeString("style", "font-weight:bold;");
            html.WriteString(link.Individual.Name.Name);
            html.WriteEndElement();
            i++;
          }
          html.WriteString(". ");
        }
      }

      html.WriteStartElement("div");
      html.WriteAttributeString("class", "intro-timeline");

      html.WriteStartElement("div");
      html.WriteAttributeString("class", "intro-end");

      if (leftPeople.Count > 0)
        html.WriteString("← ");
      if (endYear > DateTime.Now.Year - 50)
        html.WriteString("Current");
      else
        html.WriteString(Decade(endYear) + "s");
      html.WriteEndElement();

      html.WriteStartElement("div");
      html.WriteAttributeString("class", "intro-fill");

      html.WriteStartElement("div");
      html.WriteAttributeString("style", "max-width:2.75in");
      RenderLinks(leftPeople);
      html.WriteEndElement();

      html.WriteStartElement("div");
      html.WriteAttributeString("style", "flex:1");
      html.WriteEndElement();

      html.WriteStartElement("div");
      html.WriteAttributeString("style", "max-width:2.75in;text-align:right;");
      RenderLinks(rightPeople);
      html.WriteEndElement();

      html.WriteEndElement();

      html.WriteStartElement("div");
      html.WriteAttributeString("class", "intro-start");
      html.WriteString(Decade(startYear) + "s");
      if (rightPeople.Count > 0)
        html.WriteString(" →");
      html.WriteEndElement();

      html.WriteEndElement();
    }

    private static int Decade(int year)
    {
      return year - year % 10;
    }

    private record PersonLink(bool Left, Individual Individual, ISection Section);

    private static IEnumerable<PersonLink> CreateLinks(IGrouping<Individual, ISection> entry, IFamilySection current)
    {
      var left = true;
      foreach (var section in entry.OfType<IFamilySection>())
      {
        if (section == current)
          left = false;
        else
          yield return new PersonLink(left, entry.Key, section);
      }
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

      RenderIntro(this, html, renderer);

      var baseDirectory = Path.GetDirectoryName(renderer.Database.BasePath);

      html.WriteStartElement("div");
      html.WriteAttributeString("class", "diagrams");

      html.WriteStartElement("figure");
      html.WriteAttributeString("class", "decendants");
      var decendantRenderer = new DescendantLayout()
      {
        Graphics = renderer.Graphics
      };
      decendantRenderer.Render(Families, baseDirectory, HighlightsOnly ? renderer.DirectAncestors : null).WriteTo(html);
      html.WriteEndElement();

      var mapRenderer = new MapRenderer();
      if (mapRenderer.TryRender(Families, baseDirectory, out var figures))
      {
        foreach (var figure in figures)
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
      }

      var timelineRenderer = new TimelineRenderer()
      {
        Graphics = renderer.Graphics
      };
      var timelineSvg = timelineRenderer.Render(Families, baseDirectory, HighlightsOnly ? renderer.DirectAncestors : null);
      if (timelineSvg != null)
      {
        html.WriteStartElement("figure");
        html.WriteAttributeString("class", "timeline");
        timelineSvg.WriteTo(html);
        html.WriteEndElement();
        html.WriteEndElement();
      }

      foreach (var family in Families)
      {
        var allEvents = ResolvedEventGroup.Group(family.Events);

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

    private const double targetHeight = 3 * 96;
    private const double lineWidth = 7.5 * 96 - 20;
    private const double gap = 8;
    private const double maxHeight = 3.3 * 96;
    // 577

    internal static void RenderGallery(HtmlTextWriter html, IEnumerable<Media> media, IGraphics graphics)
    {
      var articles = media
        .Where(m => string.IsNullOrEmpty(m.Src)
          && (!string.IsNullOrEmpty(m.Description) || !string.IsNullOrEmpty(m.Content))
          && !(m.Attributes.TryGetValue("hidden", out var hidden) && hidden == "true"))
        .OrderBy(m => m.TopicDate)
        .ToList();
      foreach (var article in articles)
      {
        html.WriteStartElement("article");
        html.WriteRaw(Markdig.Markdown.ToHtml(article.Content ?? article.Description));
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

      var rows = new List<Row>();
      Row processRow(IEnumerable<ImageBox> images, int count)
      {
        var row = new Row();
        if (count == 1
          && (lineWidth - images.Last().ImageWidth) > 120)
        {
          row.Boxes.AddRange(images);
          images.Last().IncludeCaption = false;
          row.Boxes.Add(new CaptionBox()
          {
            Top = images.Last().Media,
            Height = images.Last().Height,
            Width = Math.Min(120, Math.Floor(lineWidth - images.Last().ImageWidth - gap))
          });
          return row;
        }
        else if (count == 2)
        {
          var captionBoxWidth = Math.Floor(lineWidth - images.Sum(im => im.ImageWidth) - gap * count);
          if (captionBoxWidth >= 90)
          {
            foreach (var image in images)
              image.IncludeCaption = false;
            row.Boxes.Add(images.ElementAt(0));
            row.Boxes.Add(new CaptionBox()
            {
              Top = images.ElementAt(0).Media,
              Bottom = images.ElementAt(1).Media,
              Width = captionBoxWidth,
              Height = images.ElementAt(0).Height
            });
            row.Boxes.Add(images.ElementAt(1));
            return row;
          }
        }
        row.Boxes.AddRange(images);
        return row;
      }

      if (images.Count > 0)
      {
        var rowWidth = 0.0;
        var count = 0;
        var start = 0;
        for (var i = 0; i < images.Count; i++)
        {
          rowWidth += images[i].Width;
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

            // Don't shrink the photos if there is less than 60% of the last photo
            var cutoffWidth = images.Skip(start).Take(count - 1).Sum(im => im.Width)
              + images.ElementAt(start + count - 1).Width * 0.6
              + (count - 1) * gap;
            if (count > 2 && cutoffWidth > lineWidth)
            {
              count--;
              i--;
            }

            var totalWidth = images.Skip(start).Take(count).Sum(im => im.Width) + (count - 1) * gap;
            var numAttempts = 0;
            while (Math.Abs(totalWidth - lineWidth) > 2
              && numAttempts < 10
              && images.ElementAt(start).Height < maxHeight)
            {
              var factor = Math.Min(Math.Floor(lineWidth / totalWidth * 1000) / 1000, 0.99);
              foreach (var image in images.Skip(start).Take(count))
              {
                image.Height *= factor;
                image.ImageWidth *= factor;
              }
              totalWidth = images.Skip(start).Take(count).Sum(im => im.Width) + (count - 1) * gap;
              numAttempts++;
            }

            rows.Add(processRow(images.Skip(start).Take(count), count));

            start = i + 1;
            count = 0;
            rowWidth = 0;
          }
        }

        if (start < images.Count)
          rows.Add(processRow(images.Skip(start), count));
        
        foreach (var row in rows)
        {
          html.WriteStartElement("div");
          html.WriteAttributeString("class", "gallery");
          foreach (var box in row.Boxes)
            box.ToHtml(html); 
          html.WriteEndElement();
        }
      }
    }

    private class Row
    {
      public List<IBox> Boxes { get; } = new List<IBox>();
      
      public void ToHtml(XmlWriter html)
      {

      }
    }

    private interface IBox
    {
      public double Width { get; }
      public double Height { get; }

      void ToHtml(XmlWriter html);
    }

    private class CaptionBox : IBox
    {
      public Media Top { get; set; }
      public Media Bottom { get; set; }

      public double Height { get; set; }
      public double Width { get; set; }

      public void ToHtml(XmlWriter html)
      {
        html.WriteStartElement("div");
        html.WriteAttributeString("class", "caption-box");
        if (Bottom == null)
          html.WriteAttributeString("style", $"flex:1;height:{Height:0.0}px");
        else
          html.WriteAttributeString("style", $"width:{Width:0.0}px;height:{Height:0.0}px");
        WriteCaption(html, Top, "div", "◀ ", Bottom == null ? $"width:{Width:0.0}px" : null);
        if (Bottom != null)
          WriteCaption(html, Bottom, "div", " ▶", "text-align: right;margin-left: auto;");
        html.WriteEndElement();
      }
    }

    private class ImageBox : IBox
    {
      private bool _includeCaption = true;

      public Media Media { get; }
      public double CaptionWidth { get; private set; } = 2 * 96;
      public double ImageWidth { get; set; }
      public double Height { get; set; }
      public double Width => Math.Max(ImageWidth, CaptionWidth);
      public bool IncludeCaption
      {
        get => _includeCaption;
        set
        {
          _includeCaption = value;
          if (!value)
            CaptionWidth = 0;
        }
      }

      public ImageBox(Media media, IGraphics graphics)
      {
        Media = media;
        Height = targetHeight;
        if (!media.Width.HasValue || !media.Height.HasValue)
          ImageWidth = targetHeight;
        else
          ImageWidth = media.Width.Value * targetHeight / media.Height.Value;
        var root = new XElement("root");
        using (var writer = root.CreateWriter())
          WriteCaption(writer, media, "div", "▲ ");
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

      public void ToHtml(XmlWriter html)
      {
        html.WriteStartElement("figure");
        html.WriteAttributeString("style", $"max-width:{Width:0.0}px;");
        html.WriteStartElement("img");
        html.WriteAttributeString("src", Media.Src);
        html.WriteAttributeString("style", $"width:{ImageWidth:0.0}px;height:{Height:0.0}px");
        html.WriteEndElement();
        if (IncludeCaption)
          WriteCaption(html, Media, "figcaption", "▲ ");
        html.WriteEndElement();
      }
    }

    private static void WriteCaption(XmlWriter html, Media media, string elementName, string prefixSuffix, string style = null)
    {
      if (!string.IsNullOrEmpty(media.Description)
        || media.Date.HasValue
        || media.Place != null)
      {
        html.WriteStartElement(elementName);
        if (!string.IsNullOrEmpty(style))
          html.WriteAttributeString("style", style);
        if (prefixSuffix?.EndsWith(" ") == true)
          html.WriteString(prefixSuffix);
        if (media.Date.HasValue && media.Date.Start.Certainty == DateCertainty.Known)
        {
          html.WriteStartElement("time");
          html.WriteString(media.Date.ToString("yyyy MMM d"));
          html.WriteEndElement();
          html.WriteString(": ");
        }
        if (!string.IsNullOrEmpty(media.Description))
          html.WriteRaw(ParagraphBuilder.ToInlineHtml(media.Description.TrimEnd('.')));
        if (media.Place != null)
        {
          html.WriteString(" at " + media.Place.Names.FirstOrDefault()?.Name);
        }
        html.WriteString(".");
        if (prefixSuffix?.StartsWith(" ") == true)
          html.WriteString(prefixSuffix);
        html.WriteEndElement();
      }
    }
  }
}
