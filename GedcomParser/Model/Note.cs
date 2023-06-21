using System;
using System.Diagnostics;
using System.Text;

namespace GedcomParser.Model
{
  [DebuggerDisplay("{Text}")]
  public class Note : IHasId
  {
    public Identifiers Id { get; } = new Identifiers();
    public string DuplicateOf { get; set; }
    public string Text { get; set; }
    public string MimeType { get; set; }

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
