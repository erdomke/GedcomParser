using GedcomParser.Model;
using Microsoft.Msagl.Core.Geometry.Curves;
using Microsoft.Msagl.Core.Layout;
using Microsoft.Msagl.Core.Routing;
using Microsoft.Msagl.Layout.Layered;
using Microsoft.Msagl.Miscellaneous;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace GedcomParser
{
  internal class DecendantLayout
  {
    public IGraphics Graphics { get; set; }

    public XElement Render(IEnumerable<ResolvedFamily> families, string baseDirectory)
    {
      var personReference = families
        .SelectMany(f => f.Members)
        .Select(m => m.Individual)
        .Distinct()
        .OrderBy(i => i.BirthDate)
        .Select(i => new Person(i, Graphics, baseDirectory))
        .ToDictionary(p => p.Individual.Id.Primary);
      //var familyNodes = new List<Node>();

      var graph = new GeometryGraph();
      foreach (var person in personReference.Values)
        graph.Nodes.Add(person.Node);

      foreach (var family in families)
      {
        var familyNode = new Node(CurveFactory.CreateRectangle(2, 2, new Microsoft.Msagl.Core.Geometry.Point()));
        graph.Nodes.Add(familyNode);
        foreach (var parent in family.Parents)
        {
          var edge = new Edge(familyNode, personReference[parent.Id.Primary].Node);
          graph.Edges.Add(edge);
        }
        foreach (var child in family.Children(FamilyLinkType.Birth))
        {
          var edge = new Edge(personReference[child.Id.Primary].Node, familyNode);
          graph.Edges.Add(edge);
        }
      }

      LayoutHelpers.CalculateLayout(graph, new SugiyamaLayoutSettings()
      {
        NodeSeparation = 10,
        MinNodeHeight = 2,
        MinNodeWidth = 2,
        EdgeRoutingSettings = new EdgeRoutingSettings()
        {
          EdgeRoutingMode = EdgeRoutingMode.Rectilinear  
        }
      }, null);
      var result = new XElement(Svg.Ns + "svg");
      foreach (var person in personReference.Values)
        result.Add(person.ToSvg());
      var lineStyle = "stroke:black;stroke-width:1px;fill:none";
      foreach (var edge in graph.Edges)
      {
        var midY = (edge.Curve.Start.Y + edge.Curve.End.Y) / 2;
        var path = $"M {edge.Curve.Start.X} {edge.Curve.Start.Y} L {edge.Curve.Start.X} {midY} L {edge.Curve.End.X} {midY} L {edge.Curve.End.X} {edge.Curve.End.Y}";
        result.Add(new XElement(Svg.Ns + "path"
          , new XAttribute("style", lineStyle)
          , new XAttribute("d", path)));
      }

      var left = personReference.Values.Min(p => p.Node.Center.X - p.Node.Width / 2);
      var right = personReference.Values.Max(p => p.Node.Center.X + p.Node.Width / 2);
      var top = personReference.Values.Min(p => p.Node.Center.Y - p.Node.Height / 2);
      var bottom = personReference.Values.Max(p => p.Node.Center.Y + p.Node.Height / 2);
      var height = bottom - top;
      var width = right - left;
      result.SetAttributeValue("viewBox", $"{left} {top} {width} {height}");
      result.SetAttributeValue("style", $"width:{width}px;height:{height};");
      return result;
    }

    private class Person
    {
      private const int MaxImageHeight = 96;
      private Size _imageSize;
      private List<(string, Size, double)> _lines = new List<(string, Size, double)>();

      public Individual Individual { get; }
      public Node Node { get; }
      public string ImagePath { get; }

      public Person(Individual individual, IGraphics graphics, string baseDirectory)
      {
        Individual = individual;
        if (string.IsNullOrEmpty(individual.Picture?.Src))
        {
          _imageSize = new Size(MaxImageHeight * 0.75, MaxImageHeight);
        }
        else
        {
          ImagePath = Path.Combine(baseDirectory, individual.Picture.Src);
          using (var stream = File.OpenRead(ImagePath))
          {
            var size = graphics.MeasureImage(stream);
            if (size.Height != MaxImageHeight)
              _imageSize = new Size(size.Width * MaxImageHeight / size.Height, MaxImageHeight);
            else
              _imageSize = size;
          }
        }

        var style = ReportStyle.Default;
        var name = individual.Name;
        if (name.SurnameStart > 0)
        {
          _lines.Add(Measure(name.Name.Substring(0, name.SurnameStart).Trim(), graphics, style, style.BaseFontSize));
          _lines.Add(Measure(name.Name.Substring(name.SurnameStart).Trim(), graphics, style, style.BaseFontSize));
        }
        if (individual.BirthDate.HasValue)
          _lines.Add(Measure("B: " + individual.BirthDate.ToString("s"), graphics, style, style.BaseFontSize - 4));
        if (individual.DeathDate.HasValue)
          _lines.Add(Measure("D: " + individual.BirthDate.ToString("s"), graphics, style, style.BaseFontSize - 4));
        else if (individual.Events.Any(e => e.Type == EventType.Death || e.Type == EventType.Burial))
          _lines.Add(Measure("Deceased", graphics, style, style.BaseFontSize - 4));
        var height = _lines.Select(l => l.Item2.Height).Append(_imageSize.Height).Sum();
        var width = _lines.Select(l => l.Item2.Width).Append(_imageSize.Width).Max();
        
        Node = new Node(CurveFactory.CreateRectangle(width, height, new Microsoft.Msagl.Core.Geometry.Point()));
      }

      private (string, Size, double) Measure(string text, IGraphics graphics, ReportStyle style, double fontSize)
      {
        return (text, graphics.MeasureText(style.FontName, fontSize, text), fontSize);
      }

      public XElement ToSvg()
      {
        var group = new XElement(Svg.Ns + "g"
          , new XAttribute("transform", $"translate({Node.Center.X - Node.Width / 2},{Node.Center.Y - Node.Height / 2})")
        );
        if (string.IsNullOrEmpty(ImagePath))
        {
          group.Add(new XElement(Svg.Ns + "rect"
            , new XAttribute("x", Node.Width / 2 - _imageSize.Width / 2)
            , new XAttribute("y", 0)
            , new XAttribute("width", _imageSize.Width)
            , new XAttribute("height", _imageSize.Height)
            , new XAttribute("style", "fill:#eee;")));
        }
        else
        {
          group.Add(new XElement(Svg.Ns + "image"
            , new XAttribute("href", new Uri(ImagePath).ToString())
            , new XAttribute("x", Node.Width / 2 - _imageSize.Width / 2)
            , new XAttribute("y", 0)
            , new XAttribute("width", _imageSize.Width)
            , new XAttribute("height", _imageSize.Height)));
        }
        var bottom = _imageSize.Height;
        foreach (var line in _lines)
        {
          bottom += line.Item2.Height;
          group.Add(new XElement(Svg.Ns + "text"
            , new XAttribute("x", Node.Width / 2 - line.Item2.Width / 2)
            , new XAttribute("y", bottom - 2)
            , new XAttribute("style", $"font-size:{line.Item3}px;font-family:{ReportStyle.Default.FontName}")
            , line.Item1));
        }
        return group;
      }
    }
  }
}
