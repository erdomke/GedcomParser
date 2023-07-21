using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GedcomParser.Model
{
  public class Media : IHasId, IHasAttributes, IHasCitations, IHasNotes, IHasLinks, IHasMedia
  {
    public Identifiers Id { get; } = new Identifiers();
    public string DuplicateOf { get; set; }

    public ExtendedDateRange Date { get; set; }
    public ExtendedDateRange TopicDate { get; set; }
    public Place Place { get; set; }
    public string Src { get; set; }
    public string MimeType { get; set; }
    public string Description { get; set; }
    public string Content { get; set; }
    public double? Width { get; set; }
    public double? Height { get; set; }

    public Dictionary<string, string> Attributes { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public List<Citation> Citations { get; } = new List<Citation>();
    public List<Link> Links { get; } = new List<Link>();
    public List<Note> Notes { get; } = new List<Note>();
    public List<Media> Children { get; } = new List<Media>();
    List<Media> IHasMedia.Media => Children;

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
