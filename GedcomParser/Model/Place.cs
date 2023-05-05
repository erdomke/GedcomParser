using System.Collections.Generic;
using System.Linq;

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

    public List<Citation> Citations { get; } = new List<Citation>();
    public List<Note> Notes { get; } = new List<Note>();

    public override string ToString()
    {
      return Names.FirstOrDefault();
    }
  }
}
