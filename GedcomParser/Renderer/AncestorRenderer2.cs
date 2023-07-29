using GedcomParser.Model;
using Svg;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;

namespace GedcomParser.Renderer
{
  internal class AncestorRenderer2
  {
    public IGraphics Graphics { get; set; }

    private const double verticalGap = 2;
    private const double horizontalGap = 10;
    private const double horizPadding = 2;

    public XElement Render(Database database, string root, int maxDepth = int.MaxValue)
    {
      var shapes = new List<PersonLabel>
      {
        new PersonLabel(database, root, 0, Graphics)
      };

      for (var i = 0; i < shapes.Count; i++)
      {
        var child = shapes[i];
        var nextColumn = child.Column + 1;
        if (nextColumn >= maxDepth)
          break;

        var previousInColumn = shapes.LastOrDefault(s => s.Column == nextColumn);
        child.Parents.AddRange(database.IndividualLinks(child.Individual.Id.Primary, FamilyLinkType.Birth, FamilyLinkType.Parent)
          .Select(l => new PersonLabel(database, l.Individual2, nextColumn, Graphics)));
        
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

      // Overlap columns
      var columnCount = shapes.Max(s => s.Column) + 1;
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
        var shift = Math.Min(maxShiftBeforeCollison, columnWidth) - verticalGap;
        if (shift > 0)
        {
          foreach (var shape in shapes.Where(s => s.Column > i))
          {
            shape.Left -= shift;
          }
        }
      }

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

      public PersonLabel(Database database, string id, int column, IGraphics graphics)
      {
        Individual = database.GetValue<Individual>(id);
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
          else if (parent.Left > (Left + Width + 0.1))
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
              , new XAttribute("d", $"M {Left + horizontalGap / 2} {(parent.Top < Top ? Top : Bottom)} L {Left + horizontalGap / 2} {parent.MidY} L {parent.Left - horizPadding} {parent.MidY}"));

          }
        }
      }
    }
  }
}
