using GedcomParser.Model;
using GedcomParser.Renderer;
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

    private static IEnumerable<Individual> OrderedIndividuals(IEnumerable<ResolvedFamily> families)
    {
      var eventsByIndividualId = TimelineRenderer.AllEvents(families)
        .SelectMany(e => e.Primary.Concat(e.Secondary).Select(i => new
        {
          IndividualId = i.Id.Primary,
          Event = e
        }))
        .OrderBy(i => i.Event.Event.Date)
        .ToLookup(i => i.IndividualId, i => i.Event);
      var allIndividuals = families
        .SelectMany(f => f.Members)
        .Select(m => m.Individual)
        .Distinct()
        .OrderBy(i => {
          var firstEvent = eventsByIndividualId[i.Id.Primary].FirstOrDefault();
          if (firstEvent == null)
            return new ExtendedDateRange(ExtendedDateTime.Parse(DateTime.UtcNow.ToString("s")));
          return firstEvent.Event.Date;
        })
        .ToList();

      var ordered = new List<Individual>();
      while (allIndividuals.Any(i => !ordered.Contains(i)))
      {
        ordered.Add(allIndividuals.First(i => !ordered.Contains(i)));
        for (var i = ordered.Count - 1; i < ordered.Count; i++)
        {
          var insertAt = i;
          foreach (var eventObj in eventsByIndividualId[ordered[i].Id.Primary])
          {
            var newIds = new HashSet<string>(eventObj.Primary
              .Concat(eventObj.Secondary)
              .Where(i => !ordered.Contains(i))
              .Select(i => i.Id.Primary));
            foreach (var individual in allIndividuals.Where(i => newIds.Contains(i.Id.Primary)))
            {
              insertAt++;
              ordered.Insert(insertAt, individual);
            }
          }
        }
      }
      return ordered;
    }

    public XElement Render(IEnumerable<ResolvedFamily> families, string baseDirectory)
    {
      var personReference = new Dictionary<string, Person>();
      
      var graph = new GeometryGraph();
      foreach (var person in families
        .SelectMany(f => f.Members)
        .Select(m => m.Individual)
        .Distinct()
        .Select(i => new Person(i, Graphics, baseDirectory))
        .Reverse())
      {
        graph.Nodes.Add(person.Node);
        personReference[person.Individual.Id.Primary] = person;
      }

      foreach (var family in families.Reverse())
      {
        var familyNode = new Node(CurveFactory.CreateRectangle(2, 2, new Microsoft.Msagl.Core.Geometry.Point()))
        {
          UserData = family.Family,
        };
        graph.Nodes.Add(familyNode);
        foreach (var parent in family.Parents.Reverse())
        {
          var edge = new Edge(familyNode, personReference[parent.Id.Primary].Node);
          graph.Edges.Add(edge);
        }
        foreach (var child in family.Children(FamilyLinkType.Birth).Concat(family.Children(FamilyLinkType.Pet)).Reverse())
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

      var familyIndices = new Dictionary<string, int>();
      var offsetUp = 0;
      var lastBottom = double.NaN;
      var rows = graph.Nodes.GroupBy(n => Math.Round(n.Center.Y, 2))
        .OrderBy(g => g.Key)
        .Select(g => g.OrderBy(n => n.Center.X).ToList())
        .ToList();
      foreach (var row in rows)
      {
        var rowTop = row.Min(n => n.Center.Y - n.Height / 2) - offsetUp;
        var i = 0;
        foreach (var node in row.OrderBy(n => n.Center.X))
        {
          var newCenterY = rowTop + node.Height / 2;
          node.Center = new Microsoft.Msagl.Core.Geometry.Point(node.Center.X, newCenterY);
          if (node.UserData is Family family)
            familyIndices[family.Id.Primary] = i;
          i++;
        }

        lastBottom = row.Max(n => n.Center.Y + n.Height / 2);
        offsetUp += 5;
      }

      const double edgeSpacing = 2.0;
      var result = new XElement(SvgUtil.Ns + "svg");
      foreach (var person in personReference.Values)
        result.Add(person.ToSvg());
      var lineStyle = "stroke:black;stroke-width:1px;fill:none";
      foreach (var edgeGroup in graph.Edges.GroupBy(e => e.Target))
      {
        var edgeGroupList = edgeGroup.OrderBy(e => e.Source.Center.X).ToList();
        var familyToParent = edgeGroupList[0].Source.UserData is Family;
        var index = 0;
        var offset = -1 * edgeGroupList.Count / 2.0;
        var endXs = edgeGroupList
          .Select(e =>
          {
            if (!familyToParent)
              return e.Target.Center.X;
            var left = e.Target.Center.X - e.Target.Width * 0.4;
            var right = e.Target.Center.X + e.Target.Width * 0.4;
            if (left <= e.Source.Center.X && e.Source.Center.X <= right)
              return e.Source.Center.X;
            return double.NaN;
          })
          .ToList();
        var start = endXs.FindIndex(x => !double.IsNaN(x));
        if (start == -1)
        {
          endXs = Enumerable.Range(-1 * endXs.Count / 2, endXs.Count)
            .Select(i => edgeGroupList[0].Target.Center.X + i * edgeSpacing)
            .ToList();
        }
        else if (start > 0)
        {
          for (var i = start - 1; i >= 0; i--)
          {
            endXs[i] = endXs[i + 1] - edgeSpacing;
          }
        }
        start = endXs.FindIndex(x => double.IsNaN(x));
        if (start >= 0)
        {
          for (var i = start; i < endXs.Count; i++)
          {
            endXs[i] = endXs[i - 1] + edgeSpacing;
          }
        }

        foreach (var edge in edgeGroupList)
        {
          var id = ((IHasId)edge.Target.UserData).Id.Primary + "--" + ((IHasId)edge.Source.UserData).Id.Primary;
          var startX = edge.Source.UserData is Family ? edge.Source.Center.X : edge.Curve.Start.X;
          var startY = edge.Source.Center.Y - (edge.Source.UserData is Family ? 0 : edge.Source.Height / 2 + 2);
          //var endX = edge.Target.Width < 3 ? edge.Target.Center.X : edge.Curve.End.X;
          var endX = endXs[index];
          var endY = edge.Target.Center.Y + (edge.Target.UserData is Family ? 0 : edge.Target.Height / 2 + 2);
          var midY = startY - 12.5 + (familyToParent 
            ? offset * edgeSpacing
            : familyIndices[((IHasId)edge.Target.UserData).Id.Primary] * edgeSpacing) - 5;
          var path = $"M {startX} {startY} L {startX} {midY} L {endX} {midY} L {endX} {endY}";
          result.Add(new XElement(SvgUtil.Ns + "path"
            , new XAttribute("id", id)
            , new XAttribute("style", lineStyle)
            , new XAttribute("d", path)));
          index++;
          offset++;
        }
      }
      foreach (var node in graph.Nodes.Where(n => n.Width < 3))
      {
        result.Add(new XElement(SvgUtil.Ns + "circle"
          , new XAttribute("id", "tree-" + ((IHasId)node.UserData).Id.Primary)
          , new XAttribute("style", "fill:black")
          , new XAttribute("cx", node.Center.X)
          , new XAttribute("cy", node.Center.Y)
          , new XAttribute("r", 2)));
      }

      var left = graph.Nodes.Min(n => n.Center.X - n.Width / 2);
      var right = graph.Nodes.Max(n => n.Center.X + n.Width / 2);
      var top = graph.Nodes.Min(p => p.Center.Y - p.Height / 2);
      var bottom = graph.Nodes.Max(p => p.Center.Y + p.Height / 2) + 2;
      var height = bottom - top;
      var width = right - left;
      result.SetAttributeValue("viewBox", $"{left} {top} {width} {height}");
      result.SetAttributeValue("style", $"width:{width}px;height:{height};max-width:7.5in");
      return result;
    }

    private class Person : Shape
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
        //{
        //  _imageSize = new Size(MaxImageHeight * 0.75, MaxImageHeight);
        //}
        //else
        if (!string.IsNullOrEmpty(individual.Picture?.Src)
          && !individual.Picture.Src.StartsWith("http://")
          && !individual.Picture.Src.StartsWith("https://"))
        {
          ImagePath = Path.Combine(baseDirectory, individual.Picture.Src);
          if (individual.Picture.Width.HasValue
            && individual.Picture.Height.HasValue)
          {
            var size = new Size(individual.Picture.Width.Value, individual.Picture.Height.Value);
            _imageSize = new Size(size.Width * MaxImageHeight / size.Height, MaxImageHeight);
          }
          else
          {
            using (var stream = File.OpenRead(ImagePath))
            {
              var size = graphics.MeasureImage(stream);
              if (size.Height != MaxImageHeight)
                _imageSize = new Size(size.Width * MaxImageHeight / size.Height, MaxImageHeight);
              else
                _imageSize = size;
            }
          }
        }

        var style = ReportStyle.Default;
        var name = individual.Name;
        if (name.SurnameStart > 0)
        {
          _lines.Add(Measure(name.Name.Substring(0, name.SurnameStart).Trim(), graphics, style, style.BaseFontSize));
          _lines.Add(Measure(name.Name.Substring(name.SurnameStart).Trim(), graphics, style, style.BaseFontSize));
        }
        else if (name.SurnameLength > 0 && name.SurnameLength < name.Name.Length)
        {
          _lines.Add(Measure(name.Name.Substring(0, name.SurnameLength).Trim(), graphics, style, style.BaseFontSize));
          _lines.Add(Measure(name.Name.Substring(name.SurnameLength).Trim(), graphics, style, style.BaseFontSize));
        }
        else
        {
          _lines.Add(Measure(name.Name.Trim(), graphics, style, style.BaseFontSize));
        }
        if (individual.BirthDate.HasValue)
          _lines.Add(Measure("B: " + individual.BirthDate.ToString("s"), graphics, style, style.BaseFontSize - 4));
        if (individual.DeathDate.HasValue)
          _lines.Add(Measure("D: " + individual.DeathDate.ToString("s"), graphics, style, style.BaseFontSize - 4));
        else if (individual.Events.Any(e => e.Type == EventType.Death || e.Type == EventType.Burial))
          _lines.Add(Measure("Deceased", graphics, style, style.BaseFontSize - 4));
        Height = _lines.Select(l => l.Item2.Height).Append(_imageSize.Height).Sum();
        Width = _lines.Select(l => l.Item2.Width).Append(_imageSize.Width).Max();
        Node = new Node(CurveFactory.CreateRectangle(Width, Height, new Microsoft.Msagl.Core.Geometry.Point()))
        {
          UserData = Individual
        };
      }

      private (string, Size, double) Measure(string text, IGraphics graphics, ReportStyle style, double fontSize)
      {
        return (text, graphics.MeasureText(style.FontName, fontSize, text), fontSize);
      }

      public override IEnumerable<XElement> ToSvg()
      {
        var group = new XElement(SvgUtil.Ns + "g"
          , new XAttribute("id", "tree-" + Individual.Id.Primary)
          , new XAttribute("transform", $"translate({Node.Center.X - Node.Width / 2},{Node.Center.Y - Node.Height / 2})")
        );
        var bottom = 0.0;
        //{
        //  group.Add(new XElement(Svg.Ns + "rect"
        //    , new XAttribute("x", Node.Width / 2 - _imageSize.Width / 2)
        //    , new XAttribute("y", 0)
        //    , new XAttribute("width", _imageSize.Width)
        //    , new XAttribute("height", _imageSize.Height)
        //    , new XAttribute("style", "fill:#eee;")));
        //}
        //else
        foreach (var line in _lines)
        {
          bottom += line.Item2.Height;
          group.Add(new XElement(SvgUtil.Ns + "text"
            , new XAttribute("x", Node.Width / 2 - line.Item2.Width / 2)
            , new XAttribute("y", bottom - 2)
            , new XAttribute("style", $"font-size:{line.Item3}px;font-family:{ReportStyle.Default.FontName}")
            , line.Item1));
        }
        if (!string.IsNullOrEmpty(ImagePath))
        {
          group.Add(new XElement(SvgUtil.Ns + "image"
            , new XAttribute("href", new Uri(ImagePath).ToString())
            , new XAttribute("x", Node.Width / 2 - _imageSize.Width / 2)
            , new XAttribute("y", bottom)
            , new XAttribute("width", _imageSize.Width)
            , new XAttribute("height", _imageSize.Height)));
          //bottom = _imageSize.Height;
        }
        yield return group;
      }
    }
  }
}
