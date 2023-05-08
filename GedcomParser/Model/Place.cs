using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GedcomParser.Model
{
  public class Place : IPrimaryObject
  {
    public Identifiers Id { get; } = new Identifiers();

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public List<string> Names { get; } = new List<string>();
    public string Country { get; set; }
    public string Locality { get; set; }
    public string StreetAddress { get; set; }
    public string PostalCode { get; set; }

    public Dictionary<string, string> Attributes { get; } = new Dictionary<string, string>();
    public List<Citation> Citations { get; } = new List<Citation>();
    public List<Link> Links { get; } = new List<Link>();
    public List<Media> Media { get; } = new List<Media>();
    public List<Note> Notes { get; } = new List<Note>();

    public string GetPreferredId(Database db)
    {
      var builder = new StringBuilder();
      foreach (var part in new[] { StreetAddress, PostalCode, Locality, Country }
        .Concat(Names.First().Split(',')))
      {
        var length = Math.Min(15, 30 - builder.Length);
        if (length <= 0)
          break;
        Utilities.AddFirstLetters(part, length, builder, true);
      }
      return builder.ToString();
    }

    public override string ToString()
    {
      return Names.FirstOrDefault();
    }
  }
}
