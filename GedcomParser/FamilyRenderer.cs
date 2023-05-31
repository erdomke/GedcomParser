using GedcomParser.Model;
using System;
using System.Collections.Generic;
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
    public int HorizontalSpacing { get; set; } = 20;
    public int VerticalSpacing { get; set; } = 10;
    public string FontName { get; set; } = "Verdana";
  }

  internal class FamilyRenderer
  {
    private static XNamespace svgNs = (XNamespace)"http://www.w3.org/2000/svg";
    private DiagramOptions _options = new DiagramOptions();

    public GetTextWidth Sizer { get; set; }

    public FamilyRenderer()
    {
      Sizer = (f, p, t) => _options.NodeWidth;
    }

    public XElement Render(IEnumerable<ResolvedFamily> families)
    {
      var result = new XElement(svgNs + "svg");

      var nodesById = new Dictionary<string, Node>();
      var previousFamilies = new List<ResolvedFamily>();
      foreach (var family in families)
      {
        var familyNodes = new List<Node>();
        var previousParent = family.Parents
          .Select(p => nodesById.TryGetValue(p.Id.Primary, out var node) ? node : null)
          .FirstOrDefault(n => n != null);
        var startAfter = default(Node);
        if (previousParent != null)
        {
          familyNodes.Add(previousParent);
          startAfter = AllChildren(previousFamilies, family.Parents)
            .Select(p => nodesById.TryGetValue(p.Id.Primary, out var node) ? node : null)
            .Where(n => n != null)
            .OrderByDescending(n => n.Bottom)
            .FirstOrDefault();
        }
        foreach (var parent in family.Parents.Where(p => !nodesById.ContainsKey(p.Id.Primary)))
        {
          var node = new Node().UpdateText(parent, Sizer, _options);
          nodesById[parent.Id.Primary] = node;
          if (previousParent == null)
          {
            node.Top = 0;
            node.Left = 0;
          }
          else
          {
            if (startAfter == null)
              node.SetTopDependency(previousParent, (source, target) => source.Top);
            else
              startAfter.InsertAfter(node, (source, target) => source.Bottom + _options.VerticalSpacing);
            node.SetLeftDependency(previousParent, (source, target) => source.Right + _options.HorizontalSpacing);
          }
          previousParent = node;
          familyNodes.Add(node);
        }

        var previousRow = familyNodes
          .First(n => n.Top == familyNodes.Last().Top);
        foreach (var child in family.Children(FamilyLinkType.Birth))
        {
          var node = new Node().UpdateText(child, Sizer, _options);
          nodesById[child.Id.Primary] = node;
          node.SetLeftDependency(familyNodes.First(), (source, target) => source.Left + _options.HorizontalSpacing);
          previousRow.InsertAfter(node, (source, target) => source.Bottom + _options.VerticalSpacing);
          previousRow = node;
        }
        previousFamilies.Add(family);
      }

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

    private class Node : Rectangle
    {
      private DiagramOptions _options;

      public string Name { get; private set; }
      public string Dates { get; private set; }

      public Node UpdateText(Individual individual, GetTextWidth sizer, DiagramOptions options)
      {
        _options = options;
        Name = individual.Name.Name;
        Dates = $"{individual.BirthDate:s} - {individual.DeathDate:s}";
        if (individual.BirthDate.TryGetDiff(individual.DeathDate, out var minAge, out var maxAge))
        {
          var age = (minAge.Years + maxAge.Years) / 2;
          Dates = age.ToString() + "y, " + Dates;
        }
        Width = Math.Max(sizer(options.FontName, 16, Name), sizer(options.FontName, 12, Dates));
        return this;
      }

      public IEnumerable<XElement> ToSvg()
      {
        yield return new XElement(svgNs + "g"
            , new XAttribute("transform", $"translate({Left},{Top})")
            , new XElement(svgNs + "text"
                , new XAttribute("x", 0)
                , new XAttribute("y", 18)
                , new XAttribute("style", "font-size:16px;font-family:" + _options.FontName)
                , Name)
            , new XElement(svgNs + "text"
                , new XAttribute("x", 0)
                , new XAttribute("y", Height - 4)
                , new XAttribute("style", "fill:#999;font-size:12px;font-family:" + _options.FontName)
                , Dates)
        );
      }
    }
  }
}
