using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GedcomParser.Model
{
  public class Organization : IPrimaryObject
  {
    public Identifiers Id { get; } = new Identifiers();

    public string Name { get; set; }
    public Place Place { get; set; }
    public string DuplicateOf { get; set; }

    public Dictionary<string, string> Attributes { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public List<Citation> Citations { get; } = new List<Citation>();
    public List<Link> Links { get; } = new List<Link>();
    public List<Media> Media { get; } = new List<Media>();
    public List<Note> Notes { get; } = new List<Note>();

    public void BuildEqualityString(StringBuilder builder, Database db)
    {
      builder.Append(Name)
        .Append(Place?.Names.FirstOrDefault());
      Utilities.BuildEqualityString(this, builder);
    }

    public string GetPreferredId(Database db)
    {
      var builder = new StringBuilder();
      Utilities.AddFirstLetters(Name, 30, builder);
      return builder.ToString();
    }
  }
}
