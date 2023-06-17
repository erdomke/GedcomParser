using GedcomParser.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Media;
using System.Xml.Linq;
using static GedcomParser.AncestorRenderer;

namespace GedcomParser
{
  class DiagramOptions
  {
    public int NodeHeight { get; set; } = 36;
    public int NodeWidth { get; set; } = 300;
    public int HorizontalSpacing { get; set; } = 30;
    public int VerticalSpacing { get; set; } = 10;
    public string FontName { get; set; } = "Verdana";
  }

  internal class LegacyFamilyRenderer
  {
    private DiagramOptions _options = new DiagramOptions();

    public IGraphics Graphics { get; set; }

    public XElement Render(IEnumerable<ResolvedFamily> families, string baseDirectory)
    {
      var result = new XElement(SvgUtil.Ns + "svg");

      var nodesById = new Dictionary<string, Node>();
      var graphics = new List<ISvgGraphic>();
      var previousFamilies = new List<ResolvedFamily>();
      var familyList = families
        .ToList();
      foreach (var family in familyList)
      {
        var familyNodes = family.Parents
          .Select(p => nodesById.TryGetValue(p.Id.Primary, out var node) ? node : null)
          .Where(n => n != null)
          .OrderBy(n => n.Left)
          .ToList();
        var previousParent = familyNodes.FirstOrDefault();
        var startAfter = default(Node);
        if (previousParent != null)
        {
          startAfter = AllChildren(previousFamilies, family.Parents)
            .Select(p => nodesById.TryGetValue(p.Id.Primary, out var node) ? node : null)
            .Where(n => n != null)
            .OrderByDescending(n => n.Bottom)
            .FirstOrDefault();
        }
        var parentAnchor = (Shape)previousParent;
        for (var i = 1; i < familyNodes.Count; i++)
        {
          graphics.Add(new Connector()
          {
            Source = familyNodes[i - 1],
            SourceHandle = Handle.MiddleRight,
            Destination = familyNodes[i],
            DestinationHandle = Handle.MiddleLeft
          });
        }

        foreach (var parent in family.Parents
          .Where(p => !nodesById.ContainsKey(p.Id.Primary))
          .OrderByDescending(p => familyList.Count(f => f.Parents.Contains(p))))
        {
          var node = new Node().UpdateContents(parent, Graphics, _options, baseDirectory);
          if (previousParent == null)
          {
            var lastFirstRow = nodesById.Values
              .Where(n => n.Top == 0)
              .OrderByDescending(n => n.Right)
              .FirstOrDefault();
            node.Top = 0;
            if (lastFirstRow == null)
              node.Left = 0;
            else
            {
              node.SetLeftDependency(lastFirstRow, (source, target) => source.Right + _options.HorizontalSpacing);
              var rowHeight = Math.Max(node.Height, nodesById.Values
                .Where(n => n.Top == 0)
                .Max(n => n.Height));
              foreach (var firstRowNode in nodesById.Values.Where(n => n.Top == 0).Append(node))
                firstRowNode.Height = rowHeight;
            }
            parentAnchor = node;
          }
          else
          {
            if (startAfter == null)
            {
              node.SetTopDependency(previousParent, (source, target) => source.Top);
              var rowHeight = Math.Max(previousParent.Height, node.Height);
              previousParent.Height = rowHeight;
              node.Height = rowHeight;
              graphics.Add(new Connector()
              {
                Source = previousParent,
                SourceHandle = Handle.MiddleRight,
                Destination = node,
                DestinationHandle = Handle.MiddleLeft
              });
            }
            else
            {
              node.InsertAfter(new[] { startAfter }, (source, target) => source.Bottom + _options.VerticalSpacing);
              graphics.Add(new Connector()
              {
                Source = previousParent,
                SourceHandle = Handle.BottomLeft,
                SourceHorizontalOffset = _options.HorizontalSpacing / 3,
                Destination = node,
                DestinationHandle = Handle.MiddleLeft
              });
              parentAnchor = new Dot();
              parentAnchor.SetLeftDependency(previousParent, (source, target) => source.Left + _options.HorizontalSpacing * 2 / 3);
              parentAnchor.SetTopDependency(node, (source, target) => source.MidY - target.Height / 2);
              graphics.Add(parentAnchor);
            }
            node.SetLeftDependency(previousParent, (source, target) => source.Right + _options.HorizontalSpacing);
          }

          nodesById[parent.Id.Primary] = node;
          previousParent = node;
          familyNodes.Add(node);
        }

        var previousRow = (IEnumerable<Shape>)familyNodes
          .Where(n => n.Top == familyNodes.Last().Top)
          .ToList();
        foreach (var child in family.Children(FamilyLinkType.Birth)
          .OrderBy(i => familyList.FindIndex(f => f.Parents.Contains(i)))
          .ThenBy(i => i.BirthDate))
        {
          var node = new Node().UpdateContents(child, Graphics, _options, baseDirectory);
          nodesById[child.Id.Primary] = node;
          node.SetLeftDependency(familyNodes.First(), (source, target) => source.Left + _options.HorizontalSpacing);
          node.InsertAfter(previousRow, (source, target) => source.Bottom + _options.VerticalSpacing);
          previousRow = new[] { node };

          graphics.Add(new Connector()
          {
            Source = parentAnchor,
            SourceHandle = parentAnchor is Dot ? Handle.MiddleCenter : Handle.BottomLeft,
            SourceHorizontalOffset = parentAnchor is Dot ? 0 : _options.HorizontalSpacing * 2 / 3,
            Destination = node,
            DestinationHandle = Handle.MiddleLeft
          });
        }
        previousFamilies.Add(family);
      }

      foreach (var connector in graphics.SelectMany(n => n.ToSvg()))
        result.Add(connector);
      foreach (var node in nodesById.Values.SelectMany(n => n.ToSvg()))
        result.Add(node);

      var height = nodesById.Values.Max(n => n.Bottom);
      var width = nodesById.Values.Max(n => n.Right);
      result.SetAttributeValue("viewBox", $"0 0 {width} {height}");
      result.SetAttributeValue("style", $"width:{width}px;height:{height};");
      return result;
    }

    private IEnumerable<Individual> AllChildren(IEnumerable<ResolvedFamily> families, IEnumerable<Individual> parents)
    {
      var result = families
        .Where(f => f.Parents.Intersect(parents).Any())
        .SelectMany(f => f.Children())
        .ToList();
      foreach (var person in result.ToList())
        result.AddRange(AllChildren(families, new[] { person }));
      return result;
    }

    private class Node : Shape
    {
      private const int ImageRightMargin = 4;
      private const int MaxImageHeight = 96;

      private DiagramOptions _options;

      private Size _imageSize;
      private double _line1Height;
      private double _line2Height;

      public string Name { get; private set; }
      public string Dates { get; private set; }
      public string ImagePath { get; private set; }

      public Node UpdateContents(Individual individual, IGraphics graphics, DiagramOptions options, string baseDirectory)
      {
        _options = options;
        Name = individual.Name.Name;
        Dates = individual.DateString;
        var nameSize = graphics.MeasureText(options.FontName, 16, Name);
        var dateSize = graphics.MeasureText(options.FontName, 12, Dates);
        Width = Math.Max(nameSize.Width, dateSize.Width);
        _line1Height = nameSize.Height;
        _line2Height = dateSize.Height;
        Height = _line1Height + _line2Height + ImageRightMargin;

        if (!string.IsNullOrEmpty(individual.Picture?.Src))
        {
          ImagePath = Path.Combine(baseDirectory, individual.Picture.Src);
          using (var stream = File.OpenRead(ImagePath))
          {
            var size = graphics.MeasureImage(stream);
            if (size.Height > MaxImageHeight)
              _imageSize = new Size(size.Width * MaxImageHeight / size.Height, MaxImageHeight);
            else
              _imageSize = size;
          }
          if (_imageSize.Height > Height)
            Height = _imageSize.Height;
          Width += _imageSize.Width + ImageRightMargin;
        }

        return this;
      }

      public override IEnumerable<XElement> ToSvg()
      {
        var group = new XElement(SvgUtil.Ns + "g"
            , new XAttribute("transform", $"translate({Left},{Top})")
        );
        var textStart = 0.0;
        if (_imageSize.Width > 0)
        {
          textStart = _imageSize.Width + ImageRightMargin;
          group.Add(new XElement(SvgUtil.Ns + "image"
            , new XAttribute("href", new Uri(ImagePath).ToString())
            , new XAttribute("x", 0)
            , new XAttribute("y", 0)
            , new XAttribute("width", _imageSize.Width)
            , new XAttribute("height", _imageSize.Height)));
        }

        group.Add(new XElement(SvgUtil.Ns + "text"
          , new XAttribute("x", textStart)
          , new XAttribute("y", _line1Height)
          , new XAttribute("style", "font-size:16px;font-family:" + _options.FontName)
          , Name));
        group.Add(new XElement(SvgUtil.Ns + "text"
          , new XAttribute("x", textStart)
          , new XAttribute("y", _line1Height + ImageRightMargin + _line2Height)
          , new XAttribute("style", "fill:#999;font-size:12px;font-family:" + _options.FontName)
          , Dates));
        yield return group;
      }
    }

    private class Dot : Shape
    {
      public Dot()
      {
        Width = 6;
        Height = 6;
      }

      public override IEnumerable<XElement> ToSvg()
      {
        yield return new XElement(SvgUtil.Ns + "ellipse"
          , new XAttribute("cx", MidX)
          , new XAttribute("cy", MidY)
          , new XAttribute("rx", Width / 2)
          , new XAttribute("ry", Height / 2)
          , new XAttribute("style", "fill:black"));
      }
    }
  }
}
