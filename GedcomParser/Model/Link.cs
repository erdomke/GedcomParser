using System;
using System.Linq;
using System.Text;

namespace GedcomParser.Model
{
  public class Link : IHasId
  {
    public Identifiers Id { get; } = new Identifiers();
    public string DuplicateOf { get; set; }

    public Uri Url { get; set; }

    public string Description { get; set; }

    public bool TryGetAbbreviaton(out Link link)
    {
      link = null;
      switch (Url?.Host ?? "")
      {
        case "www.findagrave.com":
          link = new Link()
          {
            Url = Url,
            Description = "Find a Grave: " + Url.AbsolutePath.TrimEnd('/').Split('/').Reverse().Skip(1).First()
          };
          break;
        case "www.familysearch.org":
          link = new Link()
          {
            Url = Url,
            Description = "Family Search: " + Url.AbsolutePath.TrimEnd('/').Split('/').Last()
          };
          break;
        case "www.ancestry.com":
          link = new Link()
          {
            Url = Url,
            Description = "Ancestry: " + Url.AbsolutePath.TrimEnd('/').Split('/').Last()
          };
          break;
        case "www.linkedin.com":
          link = new Link()
          {
            Url = Url,
            Description = "LinkedIn: " + Url.AbsolutePath.TrimEnd('/').Split('/').Last()
          };
          break;
      }
      return link != null;
    }


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
