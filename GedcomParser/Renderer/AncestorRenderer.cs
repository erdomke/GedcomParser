using GedcomParser.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace GedcomParser
{
  internal class AncestorRenderer
  {
    private static XNamespace svgNs = (XNamespace)"http://www.w3.org/2000/svg";
    private const int nodeHeight = 36;
    private const int nodeWidth = 300;
    private const int colSpacing = 40;
    private const int horizPadding = 2;

    public IGraphics Graphics { get; set; }

    public XElement Render(Database database, string root)
    {
      var nodes = new List<Node>
      {
          new Node()
          {
              Id = root,
              Column = 0
          }.UpdateText(database, Graphics)
      };
      for (var idx = 0; idx < nodes.Count; idx++)
      {
        foreach (var parent in database
            .IndividualLinks(nodes[idx].Id, FamilyLinkType.Birth, FamilyLinkType.Parent))
        {
          var newNode = new Node()
          {
            Id = parent.Individual2,
            Type = parent.LinkType2,
            Child = nodes[idx],
            Column = nodes[idx].Column + 1
          }.UpdateText(database, Graphics);
          nodes[idx].Parents.Add(newNode);
          nodes.Add(newNode);
        }
      }

      var columns = nodes
          .GroupBy(n => n.Column)
          .OrderBy(g => g.Key)
          .Select(g => g.ToList())
          .ToList();
      var widths = columns.Select(c => c.Max(n => n.Width)).ToList();
      for (var c = columns.Count - 1; c >= 0; c--)
      {
        for (var r = 0; r < columns[c].Count; r++)
        {
          var node = columns[c][r];
          if (r > 0)
            node.Above = columns[c][r - 1];
          if (r < columns[c].Count - 1)
            node.Below = columns[c][r + 1];

          var minY = r == 0 ? 0 : columns[c][r - 1].Bottom;
          node.Left = colSpacing * c + widths.Take(c).Sum();
          if (node.Parents.Count < 1)
          {
            node.Top = minY;
          }
          else
          {
            var parentY = node.Parents
                .Select((n, i) =>
                {
                  if (i == 0)
                    return n.Parents.LastOrDefault()?.Top ?? n.Top;
                  else if (i == node.Parents.Count - 1)
                    return n.Parents.FirstOrDefault()?.Top ?? n.Top;
                  else
                    return n.Top;
                }).Average();
            if (parentY >= minY)
            {
              node.Top = parentY;
            }
            else
            {
              node.Top = minY;
              var offset = minY - parentY;
              foreach (var toAdjust in ParentTree(columns[c + 1]
                  .SkipWhile(n => !node.Parents.Contains(n))))
              {
                toAdjust.Top += offset;
              }
            }

            var space = node.Parents.Count == 2
                ? node.Parents.Max(n => n.Top) - node.Parents.Min(n => n.Bottom)
                : 0;
            if (space >= node.Height)
            {
              node.Left = node.Parents.Min(n => n.Left) - colSpacing;
            }
          }
        }
      }

      foreach (var col in columns)
      {
        var minX = col.Min(n => n.Left);
        foreach (var node in col)
          node.Left = minX;
      }

      foreach (var node in nodes)
      {
        if (node.Parents.Count > 0)
        {
          var newX = node.Parents.Min(n => n.Left);
          if (newX > node.Right)
          {
            var rightMost = node.Right;
            if (node.Above?.Right > rightMost
                && Math.Abs(node.Above.Bottom - node.Top) < 10)
              rightMost = node.Above.Right;
            if (node.Below?.Right > rightMost
                && Math.Abs(node.Bottom - node.Below.Top) < 10)
              rightMost = node.Below.Right;
            newX = rightMost + colSpacing;
          }
          foreach (var parent in node.Parents)
          {
            parent.Left = newX;
          }
        }
      }

      var offsetY = nodes.Min(n => n.Top) * -1;
      var offsetX = nodes.Min(n => n.Left) * -1;
      if (Math.Abs(offsetY - 0) > 0.0001
          || Math.Abs(offsetX - 0) > 0.0001)
      {
        foreach (var node in nodes)
        {
          node.Left += offsetX;
          node.Top += offsetY;
        }
      }

      var result = new XElement(svgNs + "svg");
      var height = nodes.Max(n => n.Bottom);
      var width = nodes.Max(n => n.Right);
      result.SetAttributeValue("viewBox", $"0 0 {width} {height}");
      foreach (var node in nodes)
        foreach (var part in node.ToSvg())
          result.Add(part);
      return result;
    }

    private static IEnumerable<Node> ParentTree(IEnumerable<Node> nodes)
    {
      foreach (var node in nodes)
      {
        yield return node;
        foreach (var parent in ParentTree(node.Parents))
          yield return parent;
      }
    }

    private class Node : Shape
    {
      public string Id { get; set; }
      public int Column { get; set; }
      public FamilyLinkType Type { get; set; }
      public Node Child { get; set; }
      public List<Node> Parents { get; } = new List<Node>();
      public string Name { get; set; }
      public string Dates { get; set; }
      public Node Above { get; set; }
      public Node Below { get; set; }

      public Node UpdateText(Database database, IGraphics graphics)
      {
        var individual = database.GetValue<Individual>(Id);
        Name = individual.Name.Name;
        Dates = individual.DateString;
        //if (individual.BirthDate.TryGetDiff(individual.DeathDate, out var minAge, out var maxAge))
        //{
        //    var age = (minAge.Years + maxAge.Years) / 2;
        //    Dates = age.ToString() + "y, " + Dates;
        //}
        var style = ReportStyle.Default;
        Width = Math.Max(graphics.MeasureText(style.FontName, style.BaseFontSize, Name).Width, graphics.MeasureText(style.FontName, style.BaseFontSize - 4, Dates).Width);
        return this;
      }

      public override IEnumerable<XElement> ToSvg()
      {
        var style = ReportStyle.Default;
        yield return new XElement(svgNs + "g"
            , new XAttribute("transform", $"translate({Left},{Top})")
            , new XElement(svgNs + "text"
                , new XAttribute("x", 0)
                , new XAttribute("y", 18)
                , new XAttribute("style", $"font-size:{style.BaseFontSize}px;font-family:{style.FontName}")
                , Name)
            , new XElement(svgNs + "text"
                , new XAttribute("x", 0)
                , new XAttribute("y", Height - 4)
                , new XAttribute("style", $"fill:#999;font-size:{style.BaseFontSize - 4}px;font-family:{style.FontName}")
                , Dates)
        );
        var lineStyle = "stroke:black;stroke-width:1px;fill:none";
        foreach (var parent in Parents)
        {
          if (Math.Abs(parent.Top - Top) < 0.001)
          {
            yield return new XElement(svgNs + "path"
                , new XAttribute("style", lineStyle)
                , new XAttribute("d", $"M {Right + horizPadding} {MidY} L {parent.Left - horizPadding} {MidY}"));
          }
          else if (parent.Left > (Left + Width + 0.1))
          {
            var halfway = parent.Left - colSpacing / 2;
            yield return new XElement(svgNs + "path"
                , new XAttribute("style", lineStyle)
                , new XAttribute("d", $"M {Right + horizPadding} {MidY} L {halfway} {MidY} L {halfway} {parent.MidY} L {parent.Left - horizPadding} {parent.MidY}"));
          }
          else
          {
            yield return new XElement(svgNs + "path"
                , new XAttribute("style", lineStyle)
                , new XAttribute("d", $"M {Left + colSpacing / 2} {(parent.Top < Top ? Top : Bottom)} L {Left + colSpacing / 2} {parent.MidY} L {parent.Left - horizPadding} {parent.MidY}"));

          }
        }
      }
    }
  }
}
