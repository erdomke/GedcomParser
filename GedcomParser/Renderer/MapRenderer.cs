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
      var maps = new[] { "usaMercatorLow.svg" };
      _maps = maps.Select(m =>
      {
        using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("GedcomParser." + m))
          return new MercatorMap(XElement.Load(stream));
      })
      .ToList();
    }

    public bool TryRender(IEnumerable<ResolvedFamily> families, string baseDirectory, out XElement element)
    {
      var countryGroups = families
        .SelectMany(f => f.Events)
        .Where(e => e.Event.Place != null
          && (e.Event.Type == EventType.Birth
            || e.Event.Type == EventType.Death
            || e.Event.Type == EventType.Census
            || e.Event.Type == EventType.Residence))
        .Select(e => e.Event.Place)
        .Where(p => p != null)
        .SelectMany(p => p.Names)
        .Where(n => n.Parts.Any(p => p.Key == "country"))
        .GroupBy(n => n.Parts.FirstOrDefault(p => p.Key == "country").Value)
        .ToList();
      if (countryGroups.Count == 1
        && countryGroups[0].Key == "United States")
      {
        using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("GedcomParser.UnitedStatesCountyMap.svg"))
        {
          element = XElement.Load(stream);
          var countyNames = new HashSet<string>(countryGroups[0].Select(n =>
          {
            var county = n.Parts.FirstOrDefault(p => p.Key == "county").Value;
            if (county == null)
              return null;

            if (county.EndsWith(" County"))
              county = county.Substring(0, county.Length - 7);
            var state = n.Parts.FirstOrDefault(p => p.Key == "state").Value;
            if (!_stateAbbreviations.TryGetValue(state ?? "", out var abbrev))
              return null;
            return county.Replace("Saint", "St_").Replace(' ', '_') + "__" + abbrev;
          })
            .Where(n => n != null));

          foreach (var county in element.Elements(SvgUtil.Ns + "g")
            .Where(e => ((string)e.Attribute("id"))?.Length == 2)
            .Elements(SvgUtil.Ns + "path"))
          {
            if (countyNames.Contains((string)county.Attribute("id")))
            {
              county.SetAttributeValue("fill", "#666");
            }
            else
            {
              county.SetAttributeValue("fill", "#fff");
            }
          }
          
          return true;
        }
      }
      else
      {
        element = null;
        return false;
      }
       
    }

    private class MercatorMap
    {
      private static XNamespace amcharts = "http://amcharts.com/ammap";
      private XElement _map;

      public Rectangle Coordinates { get; }
      public Rectangle ViewPort { get; }

      public MercatorMap(XElement map)
      {
        var meta = map.Descendants(amcharts + "ammap").FirstOrDefault();
        Coordinates = new Rectangle()
        {
          Left = (double)meta.Attribute("leftLongitude"),
          Top = (double)meta.Attribute("topLatitude"),
          Right = (double)meta.Attribute("rightLongitude"),
          Bottom = (double)meta.Attribute("bottomLatitude")
        };
        var parts = ((string)map.Attribute("viewbox")).Split(' ', ',').Select(v => double.Parse(v.Trim())).ToList();
        ViewPort = new Rectangle()
        {
          Left = parts[0],
          Top = parts[1],
          Width = parts[2],
          Height = parts[3]
        };

        _map = map;
      }

      public bool TryRenderPlaces(IEnumerable<Place> places, out XElement map)
      {
        var pxPerLong = ViewPort.Width / Coordinates.Width;
        var pxPerLat = ViewPort.Height / Coordinates.Height;

        var matches = places
          .Where(p => p.BoundingBox.Count == 4)
          .Select(p => new Rectangle()
          {
            Left = p.BoundingBox[0],
            Top = p.BoundingBox[1],
            Right = p.BoundingBox[2],
            Bottom = p.BoundingBox[3],
          })
          .Where(r => r.MidX >= Coordinates.Left && r.MidX <= Coordinates.Right
            && r.MidY >= Coordinates.Top && r.MidY <= Coordinates.Bottom)
          .ToList();
        if (matches.Count < 1)
        {
          map = null;
          return false;
        }
        else
        {
          map = XElement.Parse(_map.ToString());
          foreach (var bounding in matches)
          {
            var diameter = Math.Max(10, Math.Max(bounding.Width * pxPerLong, bounding.Height * pxPerLat));
            map.Add(new XElement(SvgUtil.Ns + "circle"
              , new XAttribute("cx", (bounding.MidX - Coordinates.Left) * pxPerLong)
              , new XAttribute("cy", (bounding.MidY - Coordinates.Top) * pxPerLat)
              , new XAttribute("r", diameter / 2)
              , new XAttribute("fill", "black")
              , new XAttribute("opacity", "0.1")));
          }
          return true;
        }
      }
    }
  }
}
