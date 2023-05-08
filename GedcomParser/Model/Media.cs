using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GedcomParser.Model
{
  public class Media : IHasId, IHasAttributes, IHasCitations, IHasNotes
  {
    public Identifiers Id { get; } = new Identifiers();

    public ExtendedDateRange Date { get; set; }
    public Place Place { get; set; }
    public string Src { get; set; }
    public string MimeType { get; set; }
    public string Description { get; set; }

    public Dictionary<string, string> Attributes { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public List<Citation> Citations { get; } = new List<Citation>();
    public List<Note> Notes { get; } = new List<Note>();

    public string GetPreferredId(Database db)
    {
      throw new NotImplementedException();
    }
  }
}
