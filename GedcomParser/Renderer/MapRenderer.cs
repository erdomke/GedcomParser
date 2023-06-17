using GedcomParser.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Xml.Linq;

namespace GedcomParser
{
  internal class MapRenderer
  {
    //internal static void AddBounds()
    //{
    //  var doc = Svg.SvgDocument.Open("C:\\Users\\erdomke\\source\\GitHub\\GedcomParser\\GedcomParser\\UnitedStatesCountyMap.svg");
    //  foreach (var state in doc.Children.OfType<SvgGroup>().Where(g => g.ID?.Length == 2))
    //  {
    //    var stateBounds = default(RectangleF?);
    //    foreach (var path in state.Children.OfType<SvgPath>())
    //    {
    //      path.CustomAttributes["data-bounds"] = $"{path.Bounds.X:#.###} {path.Bounds.Y:#.###} {path.Bounds.Width:#.###} {path.Bounds.Height:#.###}";
    //      if (stateBounds.HasValue)
    //        stateBounds = RectangleF.Union(stateBounds.Value, path.Bounds);
    //      else
    //        stateBounds = path.Bounds;
    //    }
    //    state.CustomAttributes["data-bounds"] = $"{stateBounds.Value.X:#.###} {stateBounds.Value.Y:#.###} {stateBounds.Value.Width:#.###} {stateBounds.Value.Height:#.###}";
    //  }
    //  doc.Write("C:\\Users\\erdomke\\source\\GitHub\\GedcomParser\\GedcomParser\\UnitedStatesCountyMap2.svg");
    //}

    private static Dictionary<string, string> _stateAbbreviations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
      { "ALABAMA", "AL" },
      { "ALASKA", "AK" },
      { "AMERICAN SAMOA", "AS" },
      { "ARIZONA", "AZ" },
      { "ARKANSAS", "AR" },
      { "CALIFORNIA", "CA" },
      { "COLORADO", "CO" },
      { "CONNECTICUT", "CT" },
      { "DELAWARE", "DE" },
      { "DISTRICT OF COLUMBIA", "DC" },
      { "FLORIDA", "FL" },
      { "GEORGIA", "GA" },
      { "GUAM", "GU" },
      { "HAWAII", "HI" },
      { "IDAHO", "ID" },
      { "ILLINOIS", "IL" },
      { "INDIANA", "IN" },
      { "IOWA", "IA" },
      { "KANSAS", "KS" },
      { "KENTUCKY", "KY" },
      { "LOUISIANA", "LA" },
      { "MAINE", "ME" },
      { "MARYLAND", "MD" },
      { "MASSACHUSETTS", "MA" },
      { "MICHIGAN", "MI" },
      { "MINNESOTA", "MN" },
      { "MISSISSIPPI", "MS" },
      { "MISSOURI", "MO" },
      { "MONTANA", "MT" },
      { "NEBRASKA", "NE" },
      { "NEVADA", "NV" },
      { "NEW HAMPSHIRE", "NH" },
      { "NEW JERSEY", "NJ" },
      { "NEW MEXICO", "NM" },
      { "NEW YORK", "NY" },
      { "NORTH CAROLINA", "NC" },
      { "NORTH DAKOTA", "ND" },
      { "NORTHERN MARIANA IS", "MP" },
      { "OHIO", "OH" },
      { "OKLAHOMA", "OK" },
      { "OREGON", "OR" },
      { "PENNSYLVANIA", "PA" },
      { "PUERTO RICO", "PR" },
      { "RHODE ISLAND", "RI" },
      { "SOUTH CAROLINA", "SC" },
      { "SOUTH DAKOTA", "SD" },
      { "TENNESSEE", "TN" },
      { "TEXAS", "TX" },
      { "UTAH", "UT" },
      { "VERMONT", "VT" },
      { "VIRGINIA", "VA" },
      { "VIRGIN ISLANDS", "VI" },
      { "WASHINGTON", "WA" },
      { "WEST VIRGINIA", "WV" },
      { "WISCONSIN", "WI" },
      { "WYOMING", "WY" },
    };

    private static List<MercatorMap> _maps;

    static MapRenderer()
    {
      var maps = new[] { "usa2High.svg" }; //, "india2019High.svg" };
      _maps = maps.Select(m =>
      {
        using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("GedcomParser." + m))
          return new MercatorMap(XElement.Load(stream));
      })
      .ToList();
    }

    public bool TryRender(IEnumerable<ResolvedFamily> families, string baseDirectory, out XElement element)
    {
      var places = families
        .SelectMany(f => f.Events)
        .Where(e => e.Event.Place != null
          && (e.Event.Type == EventType.Birth
            || e.Event.Type == EventType.Death
            || e.Event.Type == EventType.Census
            || e.Event.Type == EventType.Residence))
        .Select(e => e.Event.Place)
        .Where(p => p != null)
        .ToList();
      element = _maps
        .Select(m => m.TryRenderPlaces(places, out var map) ? map : null)
        .FirstOrDefault(m => m != null);
      return element != null;
    }

    private class MercatorMap
    {
      private static XNamespace amcharts = "http://amcharts.com/ammap";
      private XElement _map;

      public CartesianRectangle Coordinates { get; }
      public ScreenRectangle ViewPort { get; }

      public MercatorMap(XElement map)
      {
        var meta = map.Descendants(amcharts + "ammap").FirstOrDefault();
        Coordinates = CartesianRectangle.FromSides(
          left: (double)meta.Attribute("leftLongitude"),
          top: (double)meta.Attribute("topLatitude"),
          right: (double)meta.Attribute("rightLongitude"),
          bottom: (double)meta.Attribute("bottomLatitude")
        );
        var parts = ((string)map.Attribute("viewBox")).Split(' ', ',').Select(v => double.Parse(v.Trim())).ToList();
        ViewPort = ScreenRectangle.FromDimensions(left: parts[0], top: parts[1], width: parts[2], height: parts[3]);
        _map = map;
      }

      public bool TryRenderPlaces(IEnumerable<Place> places, out XElement map)
      {
        var pxPerLong = ViewPort.Width / Coordinates.Width;
        var scale = ViewPort.Width / (Coordinates.Width * Math.PI / 180);
        double latitudePosition(double latitude) => scale * Math.Log(Math.Tan(Math.PI / 4 + latitude * Math.PI / 360));

        var topRef = latitudePosition(Coordinates.Top);

        var matches = places
          .Where(p => p.BoundingBox.Count == 4)
          .Select(p => CartesianRectangle.FromSides(
            left: p.BoundingBox[0],
            top: p.BoundingBox[1], 
            right: p.BoundingBox[2],
            bottom: p.BoundingBox[3]
          ))
          .Where(r => Coordinates.PointInside(r.MidX, r.MidY))
          .ToList();
        if (matches.Count < 1)
        {
          map = null;
          return false;
        }
        else
        {
          var pointBounds = default(ScreenRectangle?);
          map = XElement.Parse(_map.ToString());
          foreach (var bounding in matches)
          {
            var diameter = Math.Max(bounding.Width * pxPerLong, latitudePosition(bounding.Top) - latitudePosition(bounding.Bottom));
            var opacity = 0.5 * Math.Exp(-1 * diameter / 12) + 0.1;
            diameter = Math.Max(diameter, 6);

            var markerBounds = ScreenRectangle.FromDimensions(
              left: (bounding.MidX - Coordinates.Left) * pxPerLong + ViewPort.Left - diameter / 2,
              top: topRef - latitudePosition(bounding.MidY) + ViewPort.Top - diameter / 2,
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

          //map.SetAttributeValue("viewBox", $"{newViewPort.Left} {newViewPort.Top} {newViewPort.Width} {newViewPort.Height}");
          //map.SetAttributeValue("width", newViewPort.Width);
          //map.SetAttributeValue("height", newViewPort.Height);
          return true;
        }
      }
    }
  }
}
