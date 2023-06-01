using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace GedcomParser
{
  internal class Connector
  {
    public Rectangle Source { get; set; }
    public Handle SourceHandle { get; set; }
    public Rectangle Destination { get; set; }
    public Handle DestinationHandle { get; set; }
    public double SourceHorizontalOffset { get; set; }

    public IEnumerable<XElement> ToSvg()
    {
      var lineStyle = "stroke:black;stroke-width:1px;fill:none";
      var start = Source.Handle(SourceHandle, SourceHorizontalOffset);
      var end = Destination.Handle(DestinationHandle);
      if (start.Y == end.Y)
        yield return new XElement(Svg.Ns + "path"
          , new XAttribute("style", lineStyle)
          , new XAttribute("d", $"M {start.X} {start.Y} L {end.X} {end.Y}"));
      else
        yield return new XElement(Svg.Ns + "path"
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
    MiddleRight,
    BottomLeft,
    BottomCenter,
    BottomRight,
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

  internal class Rectangle
  {
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
    public double Right => Left + Width;
    public double Bottom => Top + Height;
    public double Top 
    {
      get { return _top; }
      set
      {
        UpdateTop(_top);
        _topSource?.Remove();
      }
    }
    public double Width { get; set; } = nodeWidth;
    public double Height { get; set; } = nodeHeight;
    public double MidX => Left + Width / 2;
    public double MidY => Top + Height / 2;

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
        //case GedcomParser.Handle.BottomRight:
        default:
          return new Point(Right + xOffset, Bottom + yOffset);
      }
    }

    public void InsertAfter(Rectangle value, Func<Rectangle, Rectangle, double> newValue)
    {
      var existing = _dependencies.Where(d => d.Vertical && d.Target.Top > Bottom).ToList();
      foreach (var dependency in existing)
      {
        dependency.Remove();
        dependency.Target.SetTopDependency(value, dependency.NewValue);
      }
      value.SetTopDependency(this, newValue);
    }

    public void SetLeftDependency(Rectangle source, Func<Rectangle, Rectangle, double> newValue)
    {
      _leftSource?.Remove();
      _leftSource = new Dependency(source, this, true, newValue);
    }

    public void SetTopDependency(Rectangle source, Func<Rectangle, Rectangle, double> newValue)
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

    internal class Dependency
    {
      public bool Horizontal { get; }
      public Func<Rectangle, Rectangle, double> NewValue { get;}
      public Rectangle Source { get; }
      public Rectangle Target { get; }
      public bool Vertical => !Horizontal;

      public Dependency(Rectangle source, Rectangle target, bool horizontal, Func<Rectangle, Rectangle, double> newValue)
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
