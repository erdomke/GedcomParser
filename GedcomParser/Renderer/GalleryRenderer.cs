using ExCSS;
using GedcomParser.Model;
using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace GedcomParser.Renderer
{
  internal class GalleryRenderer
  {
    private const double targetHeight = 2.9 * 96;
    private const double gap = 8;
    private const double maxHeight = 3.2 * 96;
    // 577

    private static HashSet<string> _imageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
      ".png", ".gif", ".bmp", ".jpg", ".jpeg"
    };

    private interface IGallerySection
    {
      ExtendedDateRange TopicDate { get; }
      void ToHtml(HtmlTextWriter html, IGraphics graphics);
    }

    private class Article : IGallerySection
    {
      private readonly string _elementName;

      public Media Media { get; }
      public SourceListSection SourceList { get; }
      public ExtendedDateRange TopicDate => Media.TopicDate.HasValue ? Media.TopicDate : Media.Date;

      public Article(Media article, SourceListSection sourceList, string elementName)
      {
        Media = article;
        SourceList = sourceList;
        _elementName = elementName;
      }

      public void ToHtml(HtmlTextWriter html, IGraphics graphics)
      {
        html.WriteStartElement(_elementName);
        html.WriteStartElement("div");
        if (Media.Attributes.TryGetValue("columns", out var columns))
          html.WriteAttributeString("style", $"columns:{columns}");
        html.WriteRaw(Markdig.Markdown.ToHtml(Media.Content ?? Media.Description));
        html.WriteEndElement();
        if (Media.Citations.Count > 0)
        {
          html.WriteStartElement("p");
          html.WriteString("Sources: ");
          var first = true;
          foreach (var citation in Media.Citations)
          {
            if (first)
              first = false;
            else
              html.WriteString(", ");
            html.WriteStartElement("a");
            html.WriteAttributeString("href", "#" + citation.Id.Primary);
            html.WriteString("[" + SourceList.Add(citation) + "]");
            html.WriteEndElement();
          }
          html.WriteEndElement();
        }
        new Gallery(Media.Children, 7 * 96 - 20).ToHtml(html, graphics);
        html.WriteEndElement();
      }
    }

    private class Gallery : IGallerySection
    {
      public ExtendedDateRange TopicDate { get; }

      public List<Media> Applicable { get; }

      public double LineWidth { get; }

      public Gallery(IEnumerable<Media> media, double lineWidth = 0)
      {
        Applicable = media
          .Where(m => !string.IsNullOrEmpty(m.Src)
            && _imageExtensions.Contains(Path.GetExtension(m.Src))
            && !(m.Attributes.TryGetValue("hidden", out var hidden) && hidden == "true"))
          .Distinct()
          .OrderBy(m => m.Date.HasValue || m.TopicDate.HasValue ? 0 : 1)
          .ThenBy(m => m.Date.HasValue ? m.Date : m.TopicDate)
          .ToList();

        var firstMedia = Applicable.FirstOrDefault(m => m.Date.HasValue || m.TopicDate.HasValue);
        if (firstMedia != null
          && (firstMedia.Date.HasValue ? firstMedia.Date : firstMedia.TopicDate).TryGetRange(out var startDate, out var endDate))
        {
          var baseDate = (startDate ?? endDate).Value;
          var newDate = baseDate.AddDays(Applicable
            .Select(m => (m.Date.HasValue ? m.Date : m.TopicDate).TryGetRange(out var s, out var e) ? s ?? e : null)
            .Where(d => d.HasValue)
            .Select(d => (d.Value - baseDate).TotalDays)
            .Average());
          TopicDate = ExtendedDateRange.Parse(newDate.ToString("s"));
        }

        if (lineWidth <= 0)
          LineWidth = ReportStyle.Default.PageWidthInches * 96 - 20;
        else
          LineWidth = lineWidth;
      }

      public void ToHtml(HtmlTextWriter html, IGraphics graphics)
      {
        var images = Applicable
          .Select(m => new ImageBox(m, graphics))
          .ToList();

        var rows = new List<Row>();
        Row processRow(IEnumerable<ImageBox> images, int count)
        {
          var row = new Row();
          if (count == 1
            && (LineWidth - images.Last().ImageWidth) > 120)
          {
            row.Boxes.AddRange(images);
            images.Last().IncludeCaption = false;
            row.Boxes.Add(new CaptionBox()
            {
              Top = images.Last().Media,
              Height = images.Last().Height,
              Width = Math.Min(120, Math.Floor(LineWidth - images.Last().ImageWidth - gap))
            });
            return row;
          }
          else if (count == 2)
          {
            var captionBoxWidth = Math.Floor(LineWidth - images.Sum(im => im.ImageWidth) - gap * count);
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
            if (count >= 2 && rowWidth + (count - 1) * gap > LineWidth)
            {
              var minWidth = images.Skip(start).Take(count).Sum(im => im.CaptionWidth) + (count - 1) * gap;
              while (count > 2 && minWidth > LineWidth)
              {
                count--;
                i--;
                minWidth = images.Skip(start).Take(count).Sum(im => im.CaptionWidth) + (count - 1) * gap;
              }

              // Don't shrink the photos if there is less than 60% of the last photo
              var cutoffWidth = images.Skip(start).Take(count - 1).Sum(im => im.Width)
                + images.ElementAt(start + count - 1).Width * 0.6
                + (count - 1) * gap;
              if (count > 2 && cutoffWidth > LineWidth)
              {
                count--;
                i--;
              }

              var totalWidth = images.Skip(start).Take(count).Sum(im => im.Width) + (count - 1) * gap;
              var numAttempts = 0;
              while (Math.Abs(totalWidth - LineWidth) > 2
                && numAttempts < 10
                && images.ElementAt(start).Height < maxHeight)
              {
                var factor = Math.Min(Math.Floor(LineWidth / totalWidth * 1000) / 1000, 0.99);
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
    }

    internal static void Render(HtmlTextWriter html, IEnumerable<Media> media, IGraphics graphics, SourceListSection sourceList, string articleElement = "aside")
    {
      var sections = media
        .Where(m => string.IsNullOrEmpty(m.Src)
          && (!string.IsNullOrEmpty(m.Description) || !string.IsNullOrEmpty(m.Content))
          && !(m.Attributes.TryGetValue("hidden", out var hidden) && hidden == "true"))
        .OrderBy(m => m.TopicDate)
        .Select(m => (IGallerySection)new Article(m, sourceList, articleElement))
        .ToList();

      var gallery = new Gallery(media);
      if (gallery.Applicable.Count > 0)
        sections.Add(gallery);

      foreach (var section in sections.OrderBy(s => s.TopicDate))
        section.ToHtml(html, graphics);
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
      public double PreferredHeight => Math.Min(targetHeight, Media.Height.HasValue ? Media.Height.Value * 96 / 150 : targetHeight);
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
        if (Media.Attributes.TryGetValue("grayscale", out var value) && value == "true")
          html.WriteAttributeString("class", "grayscale");
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
        var hasDate = media.Date.HasValue && media.Date.Start.Certainty == DateCertainty.Known;
        var hasDescription = !string.IsNullOrEmpty(media.Description);
        var hasPlace = media.Place != null;

        html.WriteStartElement(elementName);
        if (!string.IsNullOrEmpty(style))
          html.WriteAttributeString("style", style);
        if (prefixSuffix?.EndsWith(" ") == true)
          html.WriteString(prefixSuffix);
        if (hasDate)
        {
          html.WriteStartElement("time");
          html.WriteString(media.Date.ToString("yyyy MMM d"));
          html.WriteEndElement();
        }
        if (hasDescription)
        {
          if (hasDate)
            html.WriteString(": ");
          html.WriteRaw(ParagraphBuilder.ToInlineHtml(media.Description.TrimEnd('.')));
        }
        if (hasPlace)
        {
          if (hasDescription)
            html.WriteString(" at ");
          else if (hasDate)
            html.WriteString(": ");
          html.WriteString(media.Place.Names.FirstOrDefault()?.Name);
        }
        if (hasDescription || hasPlace)
          html.WriteString(".");
        if (prefixSuffix?.StartsWith(" ") == true)
          html.WriteString(prefixSuffix);
        html.WriteEndElement();
      }
    }
  }
}
