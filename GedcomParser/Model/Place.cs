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
        public string Type { get; set; }

        public override string ToString()
        {
            return Names.FirstOrDefault();
        }
    }
}
