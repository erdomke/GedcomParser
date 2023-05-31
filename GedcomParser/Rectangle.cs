using System;
using System.Collections.Generic;
using System.Linq;

namespace GedcomParser
{
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
    public double MidY => Top + Height / 2;

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
