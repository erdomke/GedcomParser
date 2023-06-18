using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GedcomParser.Model
{
  public class Place : IPrimaryObject
  {
    public Identifiers Id { get; } = new Identifiers();
    public List<double> BoundingBox { get; } = new List<double>();
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public List<PlaceName> Names { get; } = new List<PlaceName>();
    public string DuplicateOf { get; set; }

    public Dictionary<string, string> Attributes { get; } = new Dictionary<string, string>();
    public List<Citation> Citations { get; } = new List<Citation>();
    public List<Link> Links { get; } = new List<Link>();
    public List<Media> Media { get; } = new List<Media>();
    public List<Note> Notes { get; } = new List<Note>();

    public string City => PlaceNamePart("city");
    public string County => PlaceNamePart("county");
    public string State => PlaceNamePart("state");
    public string Country => PlaceNamePart("country");

    public string PlaceNamePart(string part) => Names
      .Select(n => n.Parts.FirstOrDefault(k => k.Key == part).Value)
      .FirstOrDefault(v => !string.IsNullOrEmpty(v));

    public void BuildEqualityString(StringBuilder builder, Database db)
    {
      builder.Append(Latitude?.ToString())
        .Append(Longitude?.ToString())
        .Append(Names.FirstOrDefault());
      Utilities.BuildEqualityString(this, builder);
    }

    public string GetPreferredId(Database db)
    {
      var builder = new StringBuilder();
      var nameParts = Names.First().Parts.Select(p => p.Value).ToList();
      if (!string.IsNullOrEmpty(Names.First().Name))
        nameParts.AddRange(Names.First().Name.Split(',').Select(p => p.Trim()));

      foreach (var part in nameParts)
      {
        var length = Math.Min(15, 30 - builder.Length);
        if (length <= 0)
          break;
        Utilities.AddFirstLetters(part.Replace("Magisterial ", ""), length, builder, true);
      }
      return builder.ToString();
    }

    public override string ToString()
    {
      return Names.FirstOrDefault().Name;
    }
  }
}
