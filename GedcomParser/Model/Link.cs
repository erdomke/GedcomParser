using System;
using System.Text;

namespace GedcomParser.Model
{
  public class Link : IHasId
  {
    public Identifiers Id { get; } = new Identifiers();
    public string DuplicateOf { get; set; }

    public Uri Url { get; set; }

    public string Description { get; set; }

    public void BuildEqualityString(StringBuilder builder, Database db)
    {
      throw new NotImplementedException();
    }

    public string GetPreferredId(Database db)
    {
      throw new NotImplementedException();
    }
  }
}
