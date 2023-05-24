using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GedcomParser.Model
{
  public class Media : IHasId, IHasAttributes, IHasCitations, IHasNotes, IHasLinks
  {
    public Identifiers Id { get; } = new Identifiers();
    public string DuplicateOf { get; set; }

    public ExtendedDateRange Date { get; set; }
    public Place Place { get; set; }
    public string Src { get; set; }
    public string MimeType { get; set; }
    public string Description { get; set; }

    public Dictionary<string, string> Attributes { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public List<Citation> Citations { get; } = new List<Citation>();
    public List<Link> Links { get; } = new List<Link>();
    public List<Note> Notes { get; } = new List<Note>();

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
