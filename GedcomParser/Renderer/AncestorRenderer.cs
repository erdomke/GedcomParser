using GedcomParser.Model;
using GedcomParser.Renderer;
using Microsoft.Msagl.GraphmapsWithMesh;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;

namespace GedcomParser
{
  internal class AncestorRenderer : ISection
  {
    private Individual _root;
    private int _maxDepth;
    private Func<string, IEnumerable<Individual>> _getParents;
    private CountryTimeline _countries;

    public Individual Individual => _root;
    public IGraphics Graphics { get; set; }

    public string Title { get; private set; }

    public string Id { get; private set; }

    private const double verticalGap = 2;
    private const double horizontalGap = 20;
    private const double horizPadding = 2;

    public AncestorRenderer(Database database, string root, IEnumerable<ResolvedFamily> families = null, int maxDepth = int.MaxValue)
    {
      Initialize(database.GetValue<Individual>(root));
      _getParents = childId => database.IndividualLinks(childId, FamilyLinkType.Birth, FamilyLinkType.Parent)
        .Select(l => database.GetValue<Individual>(l.Individual2));
      _maxDepth = maxDepth;
      if (families != null)
        _countries = new CountryTimeline(database, families);
    }

    public AncestorRenderer(IEnumerable<ResolvedFamily> families, string root, int maxDepth = int.MaxValue)
    {
      Initialize(families.SelectMany(f => f.Members.Select(m => m.Individual))
        .First(i => i.Id.Contains(root)));
      _getParents = childId => families
        .Where(f => f.Members.Any(m => m.Role.HasFlag(FamilyLinkType.Child) && m.Individual.Id.Contains(childId)))
        .SelectMany(f => f.Parents);
      _maxDepth = maxDepth;
    }

    private void Initialize(Individual root)
    {
      _root = root;
      Id = "ancestors-" + root.Id.Primary;
      Title = "Ancestors of " + root.Name.Name;
    }

    private IEnumerable<PersonLabel> RenderShapes()
    {
      var shapes = new List<PersonLabel>
      {
        new PersonLabel(_root, 0, Graphics)
      };

      for (var i = 0; i < shapes.Count; i++)
      {
        var child = shapes[i];
        var nextColumn = child.Column + 1;
        if (nextColumn >= _maxDepth)
          break;

        var previousInColumn = shapes.LastOrDefault(s => s.Column == nextColumn);
        child.Parents.AddRange(_getParents(child.Individual.Id.Primary)
          .Select(i => new PersonLabel(i, nextColumn, Graphics)));

        if (child.Parents.Count > 0)
        {
          var childPreferredTop = child.PreferredParentTop;
          var requiredTop = previousInColumn == null ? 0 : previousInColumn.Bottom + verticalGap;
          var first = true;
          foreach (var parent in child.Parents)
          {
            if (first)
            {
              parent.Top = Math.Max(requiredTop, childPreferredTop);
              first = false;
            }
            else
            {
              parent.Top = previousInColumn.Bottom + verticalGap;
            }

            if (previousInColumn == null)
            {
              parent.Left = shapes.Where(s => s.Column == parent.Column - 1).Max(s => s.Right) + horizontalGap;
            }
            else
            {
              previousInColumn.NextInColumn = parent;
              parent.Left = previousInColumn.Left;
              parent.PreviousInColumn = previousInColumn;
            }
            parent.Child = child;
            previousInColumn = parent;
          }

          if (requiredTop > childPreferredTop)
          {
            child.UpdateTop(false);
          }
          shapes.AddRange(child.Parents);
        }
      }

      // Align children based on the center of the parents' trees versus the
      // center of the direct parents.
      var itemsToShift = shapes
        .Where(s => s.Parents.Count > 1
          && s.Top > s.Parents[0].Bottom + verticalGap
          && s.Bottom < s.Parents.Last().Top - verticalGap)
        .ToList();
      itemsToShift.Reverse();
      foreach (var shape in itemsToShift)
      {
        var above = shape.Parents.First().AllParentsAndSelf().OrderByDescending(s => s.Bottom).First();
        var below = shape.Parents.Last().AllParentsAndSelf().OrderBy(s => s.Top).First();
        if (above.Column < below.Column)
          below = above.NextInColumn;
        else if (below.Column < above.Column)
          above = below.PreviousInColumn;
        var newTop = Math.Min(
          shape.Parents.Last().Top - verticalGap
            , Math.Max(shape.Parents.First().Bottom + verticalGap
              , (below.Top + above.Bottom) / 2 - shape.Height / 2));
        shape.Top = newTop;
        if (shape.PreviousInColumn != null
          && shape.PreviousInColumn.Child == shape.Child
          && shape.PreviousInColumn.Parents.Count < 1)
        {
          shape.PreviousInColumn.Top = newTop - shape.PreviousInColumn.Height - verticalGap;
          shape.Child.Top = (shape.PreviousInColumn.Bottom + shape.Top) / 2 - shape.Child.Height / 2;
        }
        else if (shape.NextInColumn != null
          && shape.NextInColumn.Child == shape.Child
          && shape.NextInColumn.Parents.Count < 1)
        {
          shape.NextInColumn.Top = newTop + shape.Height + verticalGap;
          shape.Child.Top = (shape.Bottom + shape.NextInColumn.Top) / 2 - shape.Child.Height / 2;
        }
        if (shape.Child != null && shape.Child.Parents.Count == 1)
          shape.Child.Top = newTop;
      }

      for (var i = 1; i < shapes.Count; i++)
      {
        var parentGroup = new List<PersonLabel>() { shapes[i] };
        while ((i + 1) < shapes.Count && shapes[i + 1].Child == parentGroup[0].Child)
        {
          i++;
          parentGroup.Add(shapes[i]);
        }

        // Collision of lines
        var parentTop = parentGroup.Min(p => p.Top);
        var parentBottom = parentGroup.Max(p => p.Bottom);
        var closestCollision = shapes
          .Where(s => s != parentGroup[0].Child)
          .Where(r => r.Left < parentGroup[0].Left && r.Bottom >= parentTop && r.Top <= parentBottom)
          .OrderByDescending(r => r.Right)
          .FirstOrDefault();
        var furthestLeftBeforeCollision = closestCollision == null
          ? 0
          : closestCollision.Right + horizontalGap;

        // Collision of elements
        furthestLeftBeforeCollision = Math.Max(furthestLeftBeforeCollision, parentGroup.Max(s =>
        {
          var closestCollision = shapes
              .Where(r => r.Left < s.Left && r.Bottom >= s.Top && r.Top <= s.Bottom)
              .OrderByDescending(r => r.Right)
              .FirstOrDefault();
          if (closestCollision == null)
            return 0;
          return closestCollision.Right + horizontalGap;
        }));

        var childPosition = parentGroup[0].Child.Left + horizontalGap;
        var newLeft = Math.Max(furthestLeftBeforeCollision, childPosition);
        foreach (var parent in parentGroup)
          parent.Left = newLeft;
      }

      // Overlap columns
      /*var columnCount = shapes.Max(s => s.Column) + 1;
      for (var i = 0; i < columnCount; i++)
      {
        var maxShiftBeforeCollison = shapes
          .Where(s => s.Column <= i)
          .Min(s =>
          {
            var closestCollision = shapes
              .Where(r => r.Left > s.Left && r.Bottom >= s.Top && r.Top <= s.Bottom)
              .OrderBy(r => r.Left)
              .FirstOrDefault();
            if (closestCollision == null)
              return double.MaxValue;
            return closestCollision.Left - s.Right - horizontalGap;
          });
        if (maxShiftBeforeCollison <= 0)
          break;

        var columnWidth = shapes.Where(s => s.Column == i).Max(s => s.Width);
        var shift = Math.Min(maxShiftBeforeCollison, columnWidth) - horizontalGap;
        if (shift > 0)
        {
          foreach (var shape in shapes.Where(s => s.Column > i))
          {
            shape.Left -= shift;
          }
        }
      }*/

      return shapes;
    }

    private XElement Render(IEnumerable<PersonLabel> shapes)
    {
      var result = new XElement(SvgUtil.Ns + "svg");
      var height = shapes.Max(n => n.Bottom);
      var width = shapes.Max(n => n.Right);
      result.SetAttributeValue("viewBox", $"0 0 {width} {height}");
      result.SetAttributeValue("width", width);
      result.SetAttributeValue("height", height);
      foreach (var shape in shapes)
        foreach (var part in shape.ToSvg())
          result.Add(part);
      return result;
    }

    public XElement Render()
    {
      return Render(RenderShapes());
    }

    public void Render(HtmlTextWriter html, ReportRenderer renderer, RenderState state)
    {
      html.WriteStartSection(this, state);

      var shapes = RenderShapes();
      var svg = Render(shapes);
      svg.SetAttributeValue("style", $"max-width:{ReportStyle.Default.PageWidthInches}in;max-height:8.7in");
      svg.WriteTo(html);

      if (_countries != null)
      {
        html.WriteStartElement("figure");
        var countriesSvg = _countries.Render(_root.Id.Primary);
        countriesSvg.WriteTo(html);
        html.WriteStartElement("figcaption");
        html.WriteString("▲ Countries of origin: ");
        foreach (var country in _countries.LegendEntries)
        {
          if (country.Color == "white")
          {
            html.WriteStartElement("span");
            var style = $"display:inline-block;width:1em;height:1em;text-align:center;";
            html.WriteAttributeString("style", style);
            html.WriteString("?");
            html.WriteEndElement();
          }
          else
          {
            html.WriteStartElement("span");
            var style = $"display:inline-block;width:1em;height:1em;background:{country.Color}";
            html.WriteAttributeString("style", style);
            html.WriteEndElement();
          }
          html.WriteString(" " + country.Name);
          var percent = country.Percentage.ToString("0.0%");
          if (percent != "0.0%")
            html.WriteString($" ({percent})");
        }
        html.WriteEndElement();
        html.WriteEndElement();
      }

      WriteMedicalTable(html, renderer);
      WriteStatistics(html, renderer, shapes.Select(s => s.Individual).ToList(), state);

      html.WriteEndElement();
    }

    private void WriteStatistics(HtmlTextWriter html, ReportRenderer renderer, IEnumerable<Individual> ancestors, RenderState state)
    {
      var lifeSpans = ancestors
        .Where(i => i.BirthDate.Type == DateRangeType.Date && i.DeathDate.Type == DateRangeType.Date)
        .Select(i => new {
          Sex = i.Sex,
          BirthYear = i.BirthDate.Start.Year,
          LifeSpan = i.BirthDate.TryGetDiff(i.DeathDate, out var min, out var max) ? min.Years : -1
        })
        .Where(d => d.LifeSpan >= 0)
        .GroupBy(i => i.Sex)
        .Select((g, i) => new Dictionary<string, object>()
        {
          { "label", g.Key.ToString() },
          { "data", g.Select(v => new Dictionary<string, object>()
          {
            { "x", v.BirthYear },
            { "y", v.LifeSpan }
          }) },
          { "backgroundColor", ReportStyle.Default.Colors[i] }
        })
        .ToList();
      WriteChart(html, state, lifeSpans, _root.Id.Primary + "_lifespan", "Birth Year", "Lifespan (Years)", "Ancestor lifespan by birth year.");

      var marriageAges = ancestors
        .Where(i => i.BirthDate.Type == DateRangeType.Date)
        .Select(i => new {
          Individual = i,
          Families = renderer.Families.Where(f => f.Parents.Contains(i)).ToList()
        })
        .Where(i => i.Families.All(f => f.Events.Any(e => e.Event.Type == EventType.Marriage && e.Event.Date.Type == DateRangeType.Date)))
        .Select(i => new {
          Sex = i.Individual.Sex,
          BirthYear = i.Individual.BirthDate.Start.Year,
          MarriageAge = i.Individual.BirthDate.TryGetDiff(i.Families.Select(f => f.Events.First(e => e.Event.Type == EventType.Marriage).Event.Date)
            .OrderBy(d => d)
            .First(), out var min, out var max) ? min.Years : -1
        })
        .Where(d => d.MarriageAge >= 0)
        .GroupBy(i => i.Sex)
        .Select((g, i) => new Dictionary<string, object>()
        {
          { "label", g.Key.ToString() },
          { "data", g.Select(v => new Dictionary<string, object>()
          {
            { "x", v.BirthYear },
            { "y", v.MarriageAge }
          }) },
          { "backgroundColor", ReportStyle.Default.Colors[i] }
        })
        .ToList();
      WriteChart(html, state, marriageAges, _root.Id.Primary + "_marriageAge", "Birth Year", "Age of first marriage (Years)", "Marriage age by birth year.");
    }

    private void WriteChart(HtmlTextWriter html, RenderState state, List<Dictionary<string, object>> data, string id, string xTitle, string yTitle, string caption)
    {
      if (data.Count > 0)
      {
        var config = new Dictionary<string, object>()
        {
          { "type", "scatter" },
          { "data", new Dictionary<string, object>() {
            { "datasets", data }
          } },
          { "options", new Dictionary<string, object> {
            { "scales", new Dictionary<string, object> {
              { "x", new Dictionary<string, object> {
                { "display", true },
                { "title", new Dictionary<string, object> {
                  { "display", true },
                  { "text", xTitle },
                } }
              } },
              { "y", new Dictionary<string, object> {
                { "display", true },
                { "title", new Dictionary<string, object> {
                  { "display", true },
                  { "text", yTitle },
                } }
              } }
            } }
          } }
        };

        html.WriteStartElement("figure");
        html.WriteStartElement("canvas");
        html.WriteAttributeString("style", "width:7in;height:3in;");
        html.WriteAttributeString("id", id);
        html.WriteEndElement();
        state.Scripts.Add($@"class chart{id} extends Paged.Handler {{
  constructor(chunker, polisher, caller) {{
    super(chunker, polisher, caller);
  }}

  afterRendered(pages) {{
    new Chart(document.getElementById('{id}'), {JsonSerializer.Serialize(config)})
    return Promise.resolve(true);
  }}
}}
Paged.registerHandlers(chart{id});");
        html.WriteElementString("figcaption", caption);
        html.WriteEndElement();
      }
    }

    private void WriteMedicalTable(HtmlTextWriter html, ReportRenderer renderer)
    {
      var birthFamily = renderer.Families
        .FirstOrDefault(f => f.Members.Any(m => m.Individual == _root && m.Role == FamilyLinkType.Birth));

      html.WriteStartElement("table");
      html.WriteAttributeString("style", "break-inside:avoid;margin:1em 0;");
      html.WriteElementString("caption", "Family Medical History");
      html.WriteStartElement("tr");

      html.WriteStartElement("td");
      html.WriteAttributeString("style", "vertical-align:top");
      WritePersonMedical(html, _root, "Primary", true);
      WriteSiblings(html, _root, birthFamily, "Sibling");
      html.WriteEndElement();

      var parentFamilies = new List<(string, ResolvedFamily)>();
      if (birthFamily != null
        && birthFamily.Parents.Any())
      {
        html.WriteStartElement("td");
        html.WriteAttributeString("style", "vertical-align:top");
        foreach (var parent in birthFamily.Parents.OrderBy(p => p.Sex == Sex.Male ? 0 : 1))
        {
          var role = parent.Sex == Sex.Female ? "Mother" : "Father";
          WritePersonMedical(html, parent, role, true);
          var parentFamily = renderer.Families
            .FirstOrDefault(f => f.Members.Any(m => m.Individual == parent && m.Role == FamilyLinkType.Birth));
          WriteSiblings(html, parent, parentFamily, role + "'s Sibling");
          if (parentFamily != null)
            parentFamilies.Add((role, parentFamily));
        }
        html.WriteEndElement();
      }

      if (parentFamilies.Count > 0)
      {
        html.WriteStartElement("td");
        html.WriteAttributeString("style", "vertical-align:top");
        foreach (var family in parentFamilies)
        {
          foreach (var parent in family.Item2.Parents.OrderBy(p => p.Sex == Sex.Male ? 0 : 1))
          {
            var role = family.Item1 + "'s " + (parent.Sex == Sex.Female ? "Mother" : "Father");
            WritePersonMedical(html, parent, role, true);
          }
        }
        html.WriteEndElement();
      }

      html.WriteEndElement();
      html.WriteEndElement();
    }

    private void WriteSiblings(HtmlTextWriter html, Individual root, ResolvedFamily family, string role)
    {
      if (family == null)
        return;

      foreach (var sibling in family.Members
        .Where(m => m.Individual != root && m.Role == FamilyLinkType.Birth)
        .OrderBy(m => m.Order))
        WritePersonMedical(html, sibling.Individual, role);
    }

    private static Dictionary<string, string> _keyMetrics = new Dictionary<string, string>()
    {
      { "height", "Height: " },
      { "weight", "Weight: " },
      { "eye_color", "Eye Color: " },
      { "blood_type", "Blood Type: " },
    };

    private void WritePersonMedical(HtmlTextWriter html, Individual individual, string relation, bool emphasis = false)
    {
      html.WriteStartElement("div");
      if (emphasis)
        html.WriteAttributeString("style", "font-weight: bold;");
      html.WriteElementString("u", relation);
      html.WriteString(": " + individual.Name.Name);
      html.WriteEndElement();

      html.WriteStartElement("ul");
      html.WriteAttributeString("style", "padding-inline-start:1em;margin-block-start:0;");
      if (individual.BirthDate.HasValue || individual.DeathDate.HasValue)
      {
        html.WriteStartElement("li");
        html.WriteString(individual.DateString);
        if (individual.BirthDate.Type == DateRangeType.Date
          && individual.DeathDate.Type == DateRangeType.Date)
        {
          html.WriteString(ParagraphBuilder.GetAge(individual, individual.DeathDate, false));
        }
        html.WriteEndElement();
      }

      foreach (var metric in _keyMetrics)
      {
        if (individual.Attributes.TryGetValue(metric.Key, out var value))
        {
          html.WriteStartElement("li");
          html.WriteElementString("i", metric.Value);
          html.WriteString(value);
          html.WriteEndElement();
        }
      }

      var events = individual.Events
        .Where(e => e.TypeName == "Diagnosis")
        .Select(e => {
          var result = new ResolvedEvent(e);
          result.Primary.Add(individual);
          return result;
        })
        .ToList();
      var hasCause = true;
      if (!(individual.DeathDate.HasValue
        && individual.Events.FirstOrDefault(e => e.Type == EventType.Death).Attributes.TryGetValue("Cause", out var deathCause)))
      {
        hasCause = false;
        deathCause = null;
      }

      if (events.Count > 0 || hasCause)
      {
        html.WriteStartElement("li");
        var builder = new ParagraphBuilder()
        {
          PreviousSubject = new[] { individual }
        };
        foreach (var ev in events)
          builder.WriteEvent(html, ev, true);
        if (hasCause)
          html.WriteString($" The cause of death was {deathCause}.");
        html.WriteEndElement();
      }

      html.WriteEndElement();
    }

    [DebuggerDisplay("{Individual.Name.Name}")]
    private class PersonLabel : ISvgGraphic
    {
      private double _left;
      private double _top;
      private double _nameHeight;
      private double _dateHeight;

      public Individual Individual { get; }
      public int Column { get; }

      public PersonLabel PreviousInColumn { get; set; }
      public PersonLabel NextInColumn { get; set; }
      public List<PersonLabel> Parents { get; } = new List<PersonLabel>();
      public PersonLabel Child { get; set; }

      public IEnumerable<PersonLabel> AllParentsAndSelf()
      {
        var list = new List<PersonLabel>() { this };
        for (var i = 0; i < list.Count; i++)
          list.AddRange(list[i].Parents);
        return list;
      }

      public double PreferredParentTop
      {
        get
        {
          var parentTotalHeight = Parents.Sum(p => p.Height) + (Parents.Count - 1) * verticalGap;
          return (Bottom + Top) / 2 - parentTotalHeight / 2;
        }
      }

      public double Left
      {
        get => _left;
        set => _left = value;
      }
      public double Top
      {
        get => _top;
        set => _top = value;
      }
      public double Width { get; set; }
      public double Height { get; set; }
      public double MidY => Top + Height / 2;
      public double Bottom => Top + Height;
      public double Right => Left + Width;

      public PersonLabel(Individual individual, int column, IGraphics graphics)
      {
        Individual = individual;
        Column = column;
        var style = ReportStyle.Default;

        var nameSize = graphics.MeasureText(style.FontName, style.BaseFontSize, Individual.Name.Name);
        var dateSize = graphics.MeasureText(style.FontName, style.BaseFontSize - 4, Individual.DateString);
        Width = Math.Max(nameSize.Width, dateSize.Width);
        _nameHeight = nameSize.Height;
        _dateHeight = dateSize.Height;
        Height = nameSize.Height + dateSize.Height;
      }

      public void UpdateTop(bool downwardUpdate)
      {
        var newTop = double.MinValue;
        if (PreviousInColumn != null)
          newTop = Math.Max(newTop, PreviousInColumn.Bottom + verticalGap);
        if (Parents.Count == 1)
        {
          if (newTop > Parents[0].Top && newTop > _top)
            ShiftAncestors(newTop - Parents[0].Top);
          newTop = Math.Max(newTop, Parents[0].Top);
        }
        else if (Parents.Count > 1)
        {
          var parentMidTop = Parents[0].Bottom + (Parents.Last().Top - Parents[0].Bottom) / 2 - this.Height / 2;
          if (newTop > parentMidTop)
          {
            if (newTop > _top)
              ShiftAncestors(newTop - parentMidTop);
          }
          else
          {
            newTop = parentMidTop;
          }
        }
        
        if (newTop > double.MinValue && newTop > _top)
        {
          _top = newTop;
          if (NextInColumn != null)
            NextInColumn.UpdateTop(true);

          if (!downwardUpdate)
          {
            var curr = this;
            while (curr.PreviousInColumn != null
              && curr.PreviousInColumn.Child == curr.Child
              && curr.PreviousInColumn.Parents.Count < 1)
            {
              var prev = curr.PreviousInColumn;
              prev.Top = Top - prev.Height - verticalGap;
              curr = prev;
            }
          }
          if (Child != null)
            Child.UpdateTop(false);
        }
      }

      private void ShiftAncestors(double shift)
      {
        foreach (var parent in Parents)
        {
          parent._top += shift;
          parent.ShiftAncestors(shift);
        }
        var next = Parents.LastOrDefault()?.NextInColumn;
        if (next != null)
          next.UpdateTop(true);
      }

      public IEnumerable<XElement> ToSvg()
      {
        var style = ReportStyle.Default;
        yield return new XElement(SvgUtil.Ns + "g"
          , new XAttribute("id", $"ans-{Individual.Id.Primary}")
          , new XAttribute("transform", $"translate({Left},{Top})")
          , new XElement(SvgUtil.Ns + "text"
            , new XAttribute("x", 0)
            , new XAttribute("y", _nameHeight - 4)
            , new XAttribute("style", $"font-size:{style.BaseFontSize}px;font-family:{style.FontName}")
            , Individual.Name.Name)
          , new XElement(SvgUtil.Ns + "text"
            , new XAttribute("x", 0)
            , new XAttribute("y", Height - 4)
            , new XAttribute("style", $"fill:#999;font-size:{style.BaseFontSize - 4}px;font-family:{style.FontName}")
            , Individual.DateString)
        );
        var lineStyle = "stroke:black;stroke-width:1px;fill:none";
        foreach (var parent in Parents)
        {
          if (Math.Abs(parent.Top - Top) < 0.01)
          {
            yield return new XElement(SvgUtil.Ns + "path"
              , new XAttribute("style", lineStyle)
              , new XAttribute("d", $"M {Right + horizPadding} {MidY} L {parent.Left - horizPadding} {MidY}"));
          }
          else if (parent.Left > (Right + horizontalGap - 0.1))
          {
            var halfway = parent.Left - horizontalGap / 2;
            yield return new XElement(SvgUtil.Ns + "path"
              , new XAttribute("style", lineStyle)
              , new XAttribute("d", $"M {Right + horizPadding} {MidY} L {halfway} {MidY} L {halfway} {parent.MidY} L {parent.Left - horizPadding} {parent.MidY}"));
          }
          else
          {
            yield return new XElement(SvgUtil.Ns + "path"
              , new XAttribute("style", lineStyle)
              , new XAttribute("d", $"M {parent.Left - horizontalGap / 2} {(parent.Top < Top ? Top : Bottom)} L {parent.Left - horizontalGap / 2} {parent.MidY} L {parent.Left - horizPadding} {parent.MidY}"));

          }
        }
      }
    }
  }
}
