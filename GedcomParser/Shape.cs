using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;

namespace GedcomParser
{
  internal interface ISvgGraphic
  {
    IEnumerable<XElement> ToSvg();
  }

  internal class Connector: ISvgGraphic
  {
    public Shape Source { get; set; }
    public Handle SourceHandle { get; set; }
    public Shape Destination { get; set; }
    public Handle DestinationHandle { get; set; }
    public double SourceHorizontalOffset { get; set; }

    public IEnumerable<XElement> ToSvg()
    {
      var lineStyle = "stroke:black;stroke-width:1px;fill:none";
      var start = Source.Handle(SourceHandle, SourceHorizontalOffset);
      var end = Destination.Handle(DestinationHandle);
      if (start.Y == end.Y)
        yield return new XElement(SvgUtil.Ns + "path"
          , new XAttribute("style", lineStyle)
          , new XAttribute("d", $"M {start.X} {start.Y} L {end.X} {end.Y}"));
      else
        yield return new XElement(SvgUtil.Ns + "path"
          , new XAttribute("style", lineStyle)
          , new XAttribute("d", $"M {start.X} {start.Y} L {start.X} {end.Y} L {end.X} {end.Y}"));
    }
  }

  public enum Handle
  {
    TopLeft,
    TopCenter,
    TopRight,
    MiddleLeft,
    MiddleCenter,
    MiddleRight,
    BottomLeft,
    BottomCenter,
    BottomRight,
  }

  public struct Size
  {
    public double Width { get; }
    public double Height { get; }

    public Size(double width, double height)
    {
      Width = width;
      Height = height;
    }
  }

  [DebuggerDisplay("[x={Left},y={Top},w={Width},h={Height}]")]
  internal struct ScreenRectangle
  {
    public double Left { get; }
    public double Top { get; }
    public double MidX => Left + Width / 2;
    public double MidY => Top + Height / 2;
    public double Right  => Left + Width;
    public double Bottom => Top + Height;
    public double Width { get; }
    public double Height { get; }

    private ScreenRectangle(double left, double top, double width, double height)
    {
      Left = left;
      Top = top; 
      Width = width; 
      Height = height;
    }

    public bool PointInside(double x, double y)
    {
      return x >= Left && x <= Right
        && y >= Top && y <= Bottom;
    }

    public static ScreenRectangle FromDimensions(double left, double top, double width, double height)
    {
      return new ScreenRectangle(left, top, width, height);
    }

    public static ScreenRectangle FromSides(double left, double top, double right, double bottom)
    {
      return new ScreenRectangle(left, top, right - left, bottom - top);
    }

    public static ScreenRectangle Union(ScreenRectangle x, ScreenRectangle y)
    {
      return FromSides(Math.Min(x.Left, y.Left), Math.Min(x.Top, y.Top), Math.Max(x.Right, y.Right), Math.Max(x.Bottom, y.Bottom));
    }
  }

  [DebuggerDisplay("[x={Left},y={Top},w={Width},h={Height}]")]
  internal struct CartesianRectangle
  {
    public double Left { get; }
    public double Top { get; }
    public double MidX => Left + Width / 2;
    public double MidY => Top - Height / 2;
    public double Right => Left + Width;
    public double Bottom => Top - Height;
    public double Width { get; }
    public double Height { get; }

    private CartesianRectangle(double left, double top, double width, double height)
    {
      Left = left;
      Top = top;
      Width = width;
      Height = height;
    }

    public bool PointInside(double x, double y)
    {
      return x >= Left && x <= Right
        && y <= Top && y >= Bottom;
    }

    public static CartesianRectangle FromDimensions(double left, double top, double width, double height)
    {
      return new CartesianRectangle(left, top, width, height);
    }

    public static CartesianRectangle FromSides(double left, double top, double right, double bottom)
    {
      return new CartesianRectangle(left, top, right - left, top - bottom);
    }
  }

  internal struct Point
  {
    public double X { get; }
    public double Y { get; }

    public Point(double x, double y)
    {
      X = x;
      Y = y;
    }
  }

  internal abstract class Shape : ISvgGraphic
  {
    private double _height = nodeHeight;
    private double _left;
    private Dependency _leftSource;
    private double _top;
    private Dependency _topSource;
    private List<Dependency> _dependencies = new List<Dependency>();

    private const int nodeHeight = 36;
    private const int nodeWidth = 300;

    public double Left
    {
      get { return _left; }
      set
      {
        UpdateLeft(value);
        _leftSource?.Remove();
      }
    }
    public double Right
    {
      get => Left + Width;
      set => Left = value - Width;
    }

    public double Bottom
    {
      get => Top + Height;
      set => Top = value - Height;
    }
    public double Top
    {
      get { return _top; }
      set
      {
        UpdateTop(value);
        _topSource?.Remove();
      }
    }
    public double Width { get; set; } = nodeWidth;
    public double Height
    {
      get { return _height; }
      set
      {
        if (value != _height)
        {
          _height = value;
          foreach (var dependency in _dependencies.Where(d => d.Vertical && d.Target.Top > Top))
            dependency.Execute();
        }
      }
    }
    public double MidX
    {
      get => Left + Width / 2;
      set => Left = value - Width / 2;
    }
    public double MidY
    {
      get => Top + Height / 2;
      set => Top = value - Height / 2;
    }

    public Point Handle(Handle handle, double xOffset = 0, double yOffset = 0)
    {
      switch (handle)
      {
        case GedcomParser.Handle.TopLeft:
          return new Point(Left + xOffset, Top + yOffset);
        case GedcomParser.Handle.TopCenter:
          return new Point(MidX + xOffset, Top + yOffset);
        case GedcomParser.Handle.TopRight:
          return new Point(Right + xOffset, Top + yOffset);
        case GedcomParser.Handle.MiddleLeft:
          return new Point(Left + xOffset, MidY + yOffset);
        case GedcomParser.Handle.MiddleRight:
          return new Point(Right + xOffset, MidY + yOffset);
        case GedcomParser.Handle.BottomLeft:
          return new Point(Left + xOffset, Bottom + yOffset);
        case GedcomParser.Handle.BottomCenter:
          return new Point(MidX + xOffset, Bottom + yOffset);
        case GedcomParser.Handle.BottomRight:
          return new Point(Right + xOffset, Bottom + yOffset);
        default:
          return new Point(MidX + xOffset, MidY + yOffset);
      }
    }

    public void InsertAfter(IEnumerable<Shape> previous, Func<Shape, Shape, double> newValue)
    {
      var existing = previous
        .SelectMany(s => s._dependencies.Where(d => d.Vertical && d.Target.Top > d.Source.Bottom))
        .ToList();
      foreach (var dependency in existing)
      {
        dependency.Remove();
        dependency.Target.SetTopDependency(this, dependency.NewValue);
      }
      this.SetTopDependency(previous.First(), newValue);
    }

    public void SetLeftDependency(Shape source, Func<Shape, Shape, double> newValue)
    {
      _leftSource?.Remove();
      _leftSource = new Dependency(source, this, true, newValue);
    }

    public void SetTopDependency(Shape source, Func<Shape, Shape, double> newValue)
    {
      _topSource?.Remove();
      _topSource = new Dependency(source, this, false, newValue);
    }

    private void UpdateLeft(double value)
    {
      if (value != _left)
      {
        _left = value;
        foreach (var dependency in _dependencies.Where(d => d.Horizontal))
          dependency.Execute();
      }
    }

    private void UpdateTop(double value)
    {
      if (value != _top)
      {
        _top = value;
        foreach (var dependency in _dependencies.Where(d => d.Vertical))
          dependency.Execute();
      }
    }

    public abstract IEnumerable<XElement> ToSvg();

    internal class Dependency
    {
      public bool Horizontal { get; }
      public Func<Shape, Shape, double> NewValue { get;}
      public Shape Source { get; }
      public Shape Target { get; }
      public bool Vertical => !Horizontal;

      public Dependency(Shape source, Shape target, bool horizontal, Func<Shape, Shape, double> newValue)
      {
        Source = source;
        Horizontal = horizontal;
        Target = target;
        NewValue = newValue;

        Source._dependencies.Add(this);
        Execute();
      }

      public void Execute()
      {
        if (Horizontal)
          Target.UpdateLeft(NewValue(Source, Target));
        else
          Target.UpdateTop(NewValue(Source, Target));
      }

      public void Remove()
      {
        Source._dependencies.Remove(this);
        if (Horizontal)
          Target._leftSource = null;
        else
          Target._topSource = null;
      }
    }
  }
}
