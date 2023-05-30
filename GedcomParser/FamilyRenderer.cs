using GedcomParser.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using static GedcomParser.AncestorRenderer;

namespace GedcomParser
{
  internal class FamilyRenderer
  {
    private static XNamespace svgNs = (XNamespace)"http://www.w3.org/2000/svg";
    private const int nodeHeight = 36;
    private const int nodeWidth = 300;
    private const int spacing = 10;
    private const int horizPadding = 2;
    private const string fontName = "Verdana";

    public GetTextWidth Sizer { get; set; }

    public FamilyRenderer()
    {
      Sizer = (f, p, t) => nodeWidth;
    }

    public XElement Render(IEnumerable<ResolvedFamily> families)
    {
      var result = new XElement(svgNs + "svg");

      var allNodes = new List<Node>();
      foreach (var family in families)
      {
        var familyNodes = new List<Node>();
        var previousLeft = -1.0 * spacing;
        foreach (var node in family.Parents.Select(p => new Node().UpdateText(p, Sizer)))
        {
          node.Top = 0;
          node.Left = previousLeft + spacing;
          previousLeft = node.Right;
          familyNodes.Add(node);
        }

        var childLeft = (familyNodes.Max(n => n.Right) + familyNodes.Min(n => n.Left)) * .25;
        var bottom = familyNodes.Max(n => n.Bottom);
        foreach (var node in family.Members.Where(m => m.Role.HasFlag(FamilyLinkType.Birth))
          .Select(m => new Node().UpdateText(m.Individual, Sizer)))
        {
          node.Top = bottom + spacing;
          node.Left = childLeft;
          bottom = node.Bottom;
          familyNodes.Add(node);
        }
        allNodes.AddRange(familyNodes);
      }

      foreach (var node in allNodes.SelectMany(n => n.ToSvg()))
        result.Add(node);

      var height = allNodes.Max(n => n.Bottom);
      var width = allNodes.Max(n => n.Right);
      result.SetAttributeValue("viewBox", $"0 0 {width} {height}");
      result.SetAttributeValue("style", $"width:{width}px;height:{height};");
      return result;
    }

    private class Node : Rectangle
    {
      public string Name { get; private set; }
      public string Dates { get; private set; }

      public Node UpdateText(Individual individual, GetTextWidth sizer)
      {
        Name = individual.Name.Name;
        Dates = $"{individual.BirthDate:s} - {individual.DeathDate:s}";
        if (individual.BirthDate.TryGetDiff(individual.DeathDate, out var minAge, out var maxAge))
        {
          var age = (minAge.Years + maxAge.Years) / 2;
          Dates = age.ToString() + "y, " + Dates;
        }
        Width = Math.Max(sizer(fontName, 16, Name), sizer(fontName, 12, Dates));
        return this;
      }

      public IEnumerable<XElement> ToSvg()
      {
        yield return new XElement(svgNs + "g"
            , new XAttribute("transform", $"translate({Left},{Top})")
            , new XElement(svgNs + "text"
                , new XAttribute("x", 0)
                , new XAttribute("y", 18)
                , new XAttribute("style", "font-size:16px;font-family:" + fontName)
                , Name)
            , new XElement(svgNs + "text"
                , new XAttribute("x", 0)
                , new XAttribute("y", Height - 4)
                , new XAttribute("style", "fill:#999;font-size:12px;font-family:" + fontName)
                , Dates)
        );
      }
    }
  }
}
