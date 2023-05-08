using System;

namespace GedcomParser.Model
{
  public class Link : IHasId
  {
    public Identifiers Id { get; } = new Identifiers();

    public Uri Url { get; set; }

    public string Description { get; set; }

    public string GetPreferredId(Database db)
    {
      throw new NotImplementedException();
    }
  }
}
