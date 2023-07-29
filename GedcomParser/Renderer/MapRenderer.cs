using GedcomParser.Model;
using GedcomParser.Renderer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Xml.Linq;
using YamlDotNet.Core.Tokens;

namespace GedcomParser
{
  internal class MapRenderer
  {
    private static List<MercatorMap> _maps;

    static MapRenderer()
    {
      var maps = new[] { "usa2High.svg", "india2019High.svg", "europeHigh.svg" };
      _maps = maps.Select(m =>
      {
        using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("GedcomParser." + m))
          return new MercatorMap(XElement.Load(stream));
      })
      .ToList();
    }

    public bool TryRender(IEnumerable<ResolvedFamily> families, string baseDirectory, out IReadOnlyList<MapFigure> figures)
    {
      var places = families
        .SelectMany(f => f.Events)
        .Where(e => e.Event.Place != null
          && (e.Event.Type == EventType.Birth
            || e.Event.Type == EventType.Death
            || e.Event.Type == EventType.Census
            || e.Event.Type == EventType.Residence)
          && e.Event.Date.HasValue)
        .OrderBy(e => e.Event.Date)
        .Select(e => e.Event.Place)
        .Where(p => p != null)
        .ToList();
      figures = _maps
        .Select(m => m.TryRenderPlaces(places, out var map) ? map : null)
        .Where(m => m != null)
        .ToList();
      return figures.Count > 0;
    }

    private class MercatorMap
    {
      private static XNamespace amcharts = "http://amcharts.com/ammap";
      private XElement _map;
      private double _verticalScale;

      public CartesianRectangle Coordinates { get; }
      public ScreenRectangle ViewPort { get; }

      public MercatorMap(XElement map)
      {
        var parts = ((string)map.Attribute("viewBox"))
          .Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries)
          .Select(v => double.Parse(v.Trim()))
          .ToList();
        ViewPort = ScreenRectangle.FromDimensions(left: parts[0], top: parts[1], width: parts[2], height: parts[3]);
        _map = map;

        var calibrate = map.Descendants(amcharts + "calibrate").ToList();
        if (calibrate.Count >= 2)
        {
          var pictureBounds = ScreenRectangle.FromSides(
            left: calibrate.Select(c => (double)c.Attribute("x")).Min(),
            top: calibrate.Select(c => (double)c.Attribute("y")).Min(),
            right: calibrate.Select(c => (double)c.Attribute("x")).Max(),
            bottom: calibrate.Select(c => (double)c.Attribute("y")).Max()
          );
          var mapBounds = CartesianRectangle.FromSides(
            left: calibrate.Select(c => (double)c.Attribute("longitude")).Min(),
            top: calibrate.Select(c => (double)c.Attribute("latitude")).Max(),
            right: calibrate.Select(c => (double)c.Attribute("longitude")).Max(),
            bottom: calibrate.Select(c => (double)c.Attribute("latitude")).Min()
          );
          var horizScale = pictureBounds.Width / (mapBounds.Width * Math.PI / 180);
          _verticalScale = horizScale * pictureBounds.Height 
            / (CartPositionFromLatitudeDegrees(horizScale, mapBounds.Top)
              - CartPositionFromLatitudeDegrees(horizScale, mapBounds.Bottom));
          var top = -1 * CartPositionFromLatitudeDegrees(_verticalScale, mapBounds.Top);
          var offset = pictureBounds.Top - top;
          Coordinates = CartesianRectangle.FromSides(
            left: mapBounds.Left + (ViewPort.Left - pictureBounds.Left) * mapBounds.Width / pictureBounds.Width,
            top: LatitudeDegreesFromCartPosition(_verticalScale, -1 * (ViewPort.Top - offset)),
            right: mapBounds.Right + (ViewPort.Right - pictureBounds.Right) * mapBounds.Width / pictureBounds.Width,
            bottom: LatitudeDegreesFromCartPosition(_verticalScale, -1 * (ViewPort.Bottom - offset))
          );
        }
        else
        {
          var meta = map.Descendants(amcharts + "ammap").FirstOrDefault();
          Coordinates = CartesianRectangle.FromSides(
            left: (double)meta.Attribute("leftLongitude"),
            top: (double)meta.Attribute("topLatitude"),
            right: (double)meta.Attribute("rightLongitude"),
            bottom: (double)meta.Attribute("bottomLatitude")
          );
        }
      }

      private double CartPositionFromLatitudeDegrees(double scale, double latitudeDecimalDegrees)
      {
        return scale * Math.Log(Math.Tan(Math.PI / 4 + latitudeDecimalDegrees * Math.PI / 360));
      }

      private double LatitudeDegreesFromCartPosition(double scale, double y)
      {
        return (2 * Math.Atan(Math.Exp(y / scale)) - Math.PI / 2) * 180 / Math.PI;
      }

      public bool TryRenderPlaces(IEnumerable<Place> places, out MapFigure figure)
      {
        var pxPerLong = ViewPort.Width / Coordinates.Width;
        if (_verticalScale == 0)
          _verticalScale = ViewPort.Width / (Coordinates.Width * Math.PI / 180);
        var topRef = CartPositionFromLatitudeDegrees(_verticalScale, Coordinates.Top);

        var matches = places
          .Where(p => p.BoundingBox.Count == 4)
          .Select(p => new {
            Place = p,
            Box = CartesianRectangle.FromSides(
              left: p.BoundingBox[0],
              top: p.BoundingBox[1],
              right: p.BoundingBox[2],
              bottom: p.BoundingBox[3]
            )
           })
          .Where(r => Coordinates.PointInside(r.Box.MidX, r.Box.MidY))
          .ToList();
        if (matches.Count < 1)
        {
          figure = null;
          return false;
        }
        else
        {
          var pointBounds = default(ScreenRectangle?);
          var map = XElement.Parse(_map.ToString());
          foreach (var bounding in matches.Select(m => m.Box))
          {
            var diameter = Math.Max(bounding.Width * pxPerLong
              , CartPositionFromLatitudeDegrees(_verticalScale, bounding.Top)
                - CartPositionFromLatitudeDegrees(_verticalScale, bounding.Bottom));
            var opacity = 0.5 * Math.Exp(-1 * diameter / 12) + 0.1;
            diameter = Math.Max(diameter, 8);

            var markerBounds = ScreenRectangle.FromDimensions(
              left: (bounding.MidX - Coordinates.Left) * pxPerLong + ViewPort.Left - diameter / 2,
              top: topRef - CartPositionFromLatitudeDegrees(_verticalScale, bounding.MidY) + ViewPort.Top - diameter / 2,
              width: diameter,
              height: diameter
            );
            if (pointBounds.HasValue)
              pointBounds = ScreenRectangle.Union(markerBounds, pointBounds.Value);
            else
              pointBounds = markerBounds;

            map.Add(new XElement(SvgUtil.Ns + "circle"
              , new XAttribute("cx", markerBounds.MidX)
              , new XAttribute("cy", markerBounds.MidY)
              , new XAttribute("r", markerBounds.Width / 2)
              , new XAttribute("fill", "black")
              , new XAttribute("opacity", opacity.ToString())));
          }

          const double offset = 40;
          var newViewPort = ScreenRectangle.FromSides(
            left: Math.Max(pointBounds.Value.Left - offset, ViewPort.Left),
            top: Math.Max(pointBounds.Value.Top - offset, ViewPort.Top),
            right: Math.Min(pointBounds.Value.Right + offset, ViewPort.Right),
            bottom: Math.Min(pointBounds.Value.Bottom + offset, ViewPort.Bottom)
          );

          map.SetAttributeValue("viewBox", $"{newViewPort.Left} {newViewPort.Top} {newViewPort.Width} {newViewPort.Height}");
          map.SetAttributeValue("width", newViewPort.Width);
          map.SetAttributeValue("height", newViewPort.Height);
          map.SetAttributeValue("style", "max-width:7.5in");

          figure = new MapFigure()
          {
            Map = map,
            Width = newViewPort.Width
          };

          const string prefix = "Family members resided in ";

          var subdivisions = new[] { "country", "state", "county" };
          var path = new List<string>();
          var subset = matches.Select(m => m.Place);
          foreach (var subdivision in subdivisions)
          {
            var groups = subset
              .GroupBy(p => p.PlaceNamePart(subdivision) ?? "")
              .Where(g => !string.IsNullOrEmpty(g.Key))
              .ToList();
            if (groups.Count < 1)
            {
              if (path.Count > 0)
                figure.Caption = prefix + string.Join(", ", Enumerable.Reverse(path));
              break;
            }
            else if (groups.Count > 1)
            {
              figure.Caption = prefix + EnglishList(groups.Select(g => g.Key).ToList());
              if (path.Count > 0)
                figure.Caption += ", " + string.Join(", ", Enumerable.Reverse(path));
              break;
            }
            else
            {
              subset = groups[0];
              path.Add(groups[0].Key);
            }
          }

          if (string.IsNullOrEmpty(figure.Caption) && path.Count > 0)
            figure.Caption = prefix + string.Join(", ", Enumerable.Reverse(path));

          return true;
        }
      }

    }

    private static string EnglishList(IReadOnlyList<string> list, string conjunction = "and")
    {
      if (list.Count < 1)
        return string.Empty;
      else if (list.Count == 1)
        return list[0];
      else if (list.Count == 2)
        return string.Join(" " + conjunction + " ", list);
      else
        return string.Join(", ", list.Take(list.Count - 1)) + ", " + conjunction + " " + list.Last();
    }
  }
}
